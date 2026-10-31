using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RWORM.GUI.Models
{
    public class ClientModel : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _guid = string.Empty;
        private string _hostname = string.Empty;
        private string _username = string.Empty;
        private string _osVersion = string.Empty;
        private string _ipAddress = string.Empty;
        private string _countryCode = "??";
        private string _flag = "🌐";
        private bool _isOnline;
        private DateTime? _lastSeen;
        private DateTime? _createdAt;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("guid")]
        public string Guid
        {
            get => _guid;
            set { _guid = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("hostname")]
        public string Hostname
        {
            get => _hostname;
            set { _hostname = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("username")]
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("os_version")]
        public string OSVersion
        {
            get => _osVersion;
            set { _osVersion = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("ip_address")]
        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("country_code")]
        public string CountryCode
        {
            get => _countryCode;
            set
            {
                _countryCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FlagImagePath));
            }
        }

        [JsonPropertyName("flag")]
        public string Flag
        {
            get => _flag;
            set { _flag = value; OnPropertyChanged(); }
        }

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
        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("last_seen")]
        public DateTime? LastSeen
        {
            get => _lastSeen;
            set { _lastSeen = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
