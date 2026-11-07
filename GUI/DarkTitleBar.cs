using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RWORM.GUI
{
    /// <summary>
    /// Applies the dark non-client area (title bar) to every window so the
    /// Win32 caption matches the #1A1A1A theme instead of staying white.
    /// </summary>
    internal static class DarkTitleBar
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Windows 10 2004+/11, 19 on older builds.
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Apply(object sender, EventArgs e)
        {
            if (sender is not Window window) return;

            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            int on = 1;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref on, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref on, sizeof(int));
            }
        }
    }
}
