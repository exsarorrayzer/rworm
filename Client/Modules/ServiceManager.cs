using System;
using System.Runtime.InteropServices;
using System.Text;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class ServiceManager
    {
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", EntryPoint = "StartServiceW", SetLastError = true)]
        private static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, IntPtr lpServiceArgVectors);

        [DllImport("advapi32.dll", EntryPoint = "ControlService", SetLastError = true)]
        private static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumServicesStatusEx(
            IntPtr hSCManager,
            int infoLevel,
            uint dwServiceType,
            uint dwServiceState,
            IntPtr lpServices,
            uint cbBufSize,
            out uint pcbBytesNeeded,
            out uint lpServicesReturned,
            ref uint lpResumeHandle,
            string? pszGroupName);

        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_WIN32 = 0x00000030;
        private const uint SERVICE_STATE_ALL = 0x00000003;
        private const int SC_ENUM_PROCESS_INFO = 0;
        private const uint SERVICE_CONTROL_STOP = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ENUM_SERVICE_STATUS_PROCESS
        {
            public string lpServiceName;
            public string lpDisplayName;
            public SERVICE_STATUS_PROCESS ServiceStatusProcess;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
            public uint dwProcessId;
            public uint dwServiceFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        public static byte[] ListServices()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            IntPtr hSCM = OpenSCManager(null, null, 0x0004 /* SC_MANAGER_ENUMERATE_SERVICE */);
            if (hSCM == IntPtr.Zero)
            {
                sb.Append(']');
                return Encoding.UTF8.GetBytes(sb.ToString());
            }

            try
            {
                uint bytesNeeded = 0;
                uint servicesReturned = 0;
                uint resumeHandle = 0;

                EnumServicesStatusEx(hSCM, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                    IntPtr.Zero, 0, out bytesNeeded, out servicesReturned, ref resumeHandle, null);

                if (bytesNeeded > 0)
                {
                    IntPtr buf = Marshal.AllocHGlobal((int)bytesNeeded);
                    try
                    {
                        if (EnumServicesStatusEx(hSCM, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                            buf, bytesNeeded, out bytesNeeded, out servicesReturned, ref resumeHandle, null))
                        {
                            int structSize = Marshal.SizeOf(typeof(ENUM_SERVICE_STATUS_PROCESS));
                            IntPtr ptr = buf;

                            for (int i = 0; i < servicesReturned; i++)
                            {
                                var item = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(ptr);
                                string name = JsonHelper.Escape(item.lpServiceName);
                                string disp = JsonHelper.Escape(item.lpDisplayName);
                                string stat = JsonHelper.Escape(ResolveState(item.ServiceStatusProcess.dwCurrentState));
                                uint pid = item.ServiceStatusProcess.dwProcessId;

                                if (!first) sb.Append(',');
                                first = false;
                                sb.Append($"{{\"name\":\"{name}\",\"displayName\":\"{disp}\",\"status\":\"{stat}\",\"pid\":{pid}}}");

                                ptr = IntPtr.Add(ptr, structSize);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                }
            }
            catch { }
            finally
            {
                CloseServiceHandle(hSCM);
            }

            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ControlService(string serviceName, string action)
        {
            IntPtr hSCM = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (hSCM == IntPtr.Zero)
            {
                return Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"Cannot open SCM\"}");
            }

            try
            {
                IntPtr hService = OpenService(hSCM, serviceName, SERVICE_ALL_ACCESS);
                if (hService == IntPtr.Zero)
                {
                    return Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"Cannot open service\"}");
                }

                try
                {
                    bool ok = false;
                    switch (action.ToLowerInvariant())
                    {
                        case "start":
                            ok = StartService(hService, 0, IntPtr.Zero);
                            break;
                        case "stop":
                            var status = new SERVICE_STATUS();
                            ok = ControlService(hService, SERVICE_CONTROL_STOP, ref status);
                            break;
                        case "restart":
                            var st = new SERVICE_STATUS();
                            ControlService(hService, SERVICE_CONTROL_STOP, ref st);
                            System.Threading.Thread.Sleep(1000);
                            ok = StartService(hService, 0, IntPtr.Zero);
                            break;
                    }
                    return Encoding.UTF8.GetBytes($"{{\"ok\":{(ok ? "true" : "false")}}}");
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(hSCM);
            }
        }

        private static string ResolveState(uint state)
        {
            return state switch
            {
                1 => "Stopped",
                2 => "StartPending",
                3 => "StopPending",
                4 => "Running",
                5 => "ContinuePending",
                6 => "PausePending",
                7 => "Paused",
                _ => $"State_{state}"
            };
        }
    }
}
