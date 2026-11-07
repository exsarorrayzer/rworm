using System;
using System.Diagnostics;
using System.Text;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class ProcManager
    {
        public static byte[] ListProcesses()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    int pid = p.Id;
                    string name = JsonHelper.Escape(p.ProcessName);
                    long mem = p.WorkingSet64 / 1024;   
                    string title = JsonHelper.Escape(p.MainWindowTitle);

                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append($"{{\"pid\":{pid},\"name\":\"{name}\",\"mem\":{mem},\"title\":\"{title}\"}}");
                }
                catch { }
            }
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] KillProcess(int pid)
        {
            try
            {
                Process.GetProcessById(pid).Kill(entireProcessTree: true);
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
