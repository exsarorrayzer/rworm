using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RWORM.Client.Modules
{
    public static class NetMonitor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint remoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public uint owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 1)]
            public MIB_TCPROW_OWNER_PID[] table;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            int tableClass,
            uint reserved);

        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;

        public class ConnectionItem
        {
            public string Protocol { get; set; } = "TCP";
            public string LocalAddress { get; set; } = string.Empty;
            public int LocalPort { get; set; }
            public string RemoteAddress { get; set; } = string.Empty;
            public int RemotePort { get; set; }
            public string State { get; set; } = string.Empty;
            public uint Pid { get; set; }
            public string ProcessName { get; set; } = string.Empty;
        }

        public static byte[] ListConnections()
        {
            var list = new List<ConnectionItem>();
            int bufferSize = 0;

            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                uint result = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (result == 0)
                {
                    int numEntries = Marshal.ReadInt32(tcpTablePtr);
                    IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);

                    int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));

                    for (int i = 0; i < numEntries; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                        rowPtr = IntPtr.Add(rowPtr, rowSize);

                        int localPort = (row.localPort1 << 8) + row.localPort2;
                        int remotePort = (row.remotePort1 << 8) + row.remotePort2;

                        var localIp = new IPAddress(row.localAddr).ToString();
                        var remoteIp = new IPAddress(row.remoteAddr).ToString();

                        string procName = "";
                        try
                        {
                            var proc = System.Diagnostics.Process.GetProcessById((int)row.owningPid);
                            procName = proc.ProcessName;
                        }
                        catch { }

                        list.Add(new ConnectionItem
                        {
                            Protocol = "TCP",
                            LocalAddress = localIp,
                            LocalPort = localPort,
                            RemoteAddress = remoteIp,
                            RemotePort = remotePort,
                            State = ResolveState(row.state),
                            Pid = row.owningPid,
                            ProcessName = procName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { new { error = ex.Message } }));
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }

            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list));
        }

        private static string ResolveState(uint state)
        {
            return state switch
            {
                1 => "CLOSED",
                2 => "LISTENING",
                3 => "SYN_SENT",
                4 => "SYN_RCVD",
                5 => "ESTABLISHED",
                6 => "FIN_WAIT1",
                7 => "FIN_WAIT2",
                8 => "CLOSE_WAIT",
                9 => "CLOSING",
                10 => "LAST_ACK",
                11 => "TIME_WAIT",
                12 => "DELETE_TCB",
                _ => $"STATE_{state}"
            };
        }
    }
}
