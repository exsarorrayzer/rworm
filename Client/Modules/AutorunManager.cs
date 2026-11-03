using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace RWORM.Client.Modules
{
    public static class AutorunManager
    {
        public class StartupEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty; // Registry, Folder
        }

        public static byte[] ListStartupEntries()
        {
            var list = new List<StartupEntry>();

            // 1. Registry HKCU Run
            ReadRegRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", list);
            ReadRegRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", list);

            // 2. Registry HKLM Run
            ReadRegRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", list);
            ReadRegRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce", list);

            // 3. User Startup Folder
            try
            {
                var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(userStartup))
                {
                    foreach (var file in Directory.GetFiles(userStartup))
                    {
                        list.Add(new StartupEntry
                        {
                            Name = Path.GetFileName(file),
                            Command = file,
                            Location = userStartup,
                            Scope = "User Startup Folder"
                        });
                    }
                }
            }
            catch { }

            // 4. Common Startup Folder
            try
            {
                var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (Directory.Exists(commonStartup))
                {
                    foreach (var file in Directory.GetFiles(commonStartup))
                    {
                        list.Add(new StartupEntry
                        {
                            Name = Path.GetFileName(file),
                            Command = file,
                            Location = commonStartup,
                            Scope = "All Users Startup Folder"
                        });
                    }
                }
            }
            catch { }

            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list));
        }

        private static void ReadRegRun(RegistryKey root, string subKey, string scope, List<StartupEntry> list)
        {
            try
            {
                using var key = root.OpenSubKey(subKey, false);
                if (key != null)
                {
                    foreach (var valName in key.GetValueNames())
                    {
                        var val = key.GetValue(valName)?.ToString() ?? "";
                        list.Add(new StartupEntry
                        {
                            Name = valName,
                            Command = val,
                            Location = $"{root.Name}\\{subKey}",
                            Scope = scope
                        });
                    }
                }
            }
            catch { }
        }

        public static byte[] DeleteStartupEntry(string scope, string name, string location)
        {
            try
            {
                if (scope.StartsWith("HKCU"))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue(name, false);
                }
                else if (scope.StartsWith("HKLM"))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue(name, false);
                }
                else if (File.Exists(location))
                {
                    File.Delete(location);
                }
                return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true }));
            }
            catch (Exception ex)
            {
                return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
            }
        }
    }
}
