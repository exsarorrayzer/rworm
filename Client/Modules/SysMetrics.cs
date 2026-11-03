using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RWORM.Client.Modules
{
    public static class SysMetrics
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        private static long _prevIdleTime;
        private static long _prevKernelTime;
        private static long _prevUserTime;
        private static readonly object _cpuLock = new();

        public static byte[] GetMetricsJson()
        {
            int cpuUsage = GetCpuUsage();
            int ramUsage = 0;
            long totalRamMb = 0;
            long availRamMb = 0;

            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(mem))
            {
                ramUsage = (int)mem.dwMemoryLoad;
                totalRamMb = (long)(mem.ullTotalPhys / (1024 * 1024));
                availRamMb = (long)(mem.ullAvailPhys / (1024 * 1024));
            }

            long totalDiskGb = 0;
            long freeDiskGb = 0;
            try
            {
                var sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(sysDrive))
                {
                    var di = new DriveInfo(sysDrive);
                    if (di.IsReady)
                    {
                        totalDiskGb = di.TotalSize / (1024 * 1024 * 1024);
                        freeDiskGb = di.AvailableFreeSpace / (1024 * 1024 * 1024);
                    }
                }
            }
            catch { }

            long uptimeSec = (long)(Environment.TickCount64 / 1000);

            var report = new
            {
                cpu = cpuUsage,
                ram_percent = ramUsage,
                ram_total_mb = totalRamMb,
                ram_avail_mb = availRamMb,
                disk_total_gb = totalDiskGb,
                disk_free_gb = freeDiskGb,
                uptime_sec = uptimeSec
            };

            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report));
        }

        private static int GetCpuUsage()
        {
            lock (_cpuLock)
            {
                if (!GetSystemTimes(out long idle, out long kernel, out long user))
                    return 0;

                if (_prevKernelTime == 0 && _prevUserTime == 0)
                {
                    _prevIdleTime = idle;
                    _prevKernelTime = kernel;
                    _prevUserTime = user;
                    return 0;
                }

                long usrDiff = user - _prevUserTime;
                long kerDiff = kernel - _prevKernelTime;
                long idlDiff = idle - _prevIdleTime;

                long sysTotal = usrDiff + kerDiff;
                if (sysTotal <= 0) return 0;

                _prevIdleTime = idle;
                _prevKernelTime = kernel;
                _prevUserTime = user;

                long busy = sysTotal - idlDiff;
                if (busy < 0) busy = 0;

                int pct = (int)((busy * 100) / sysTotal);
                return Math.Clamp(pct, 0, 100);
            }
        }
    }
}
