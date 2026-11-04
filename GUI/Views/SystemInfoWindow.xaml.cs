using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class SystemInfoWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _clientId;

        public class DriveItem
        {
            public string Name { get; set; } = "";
            public string Format { get; set; } = "";
            public string Total { get; set; } = "";
            public string Free { get; set; } = "";
        }

        public SystemInfoWindow(ServerConnection server, string clientId)
        {
            InitializeComponent();
            _server = server;
            _clientId = clientId;

            _server.RegisterSystemInfoHandler(_clientId, OnSystemInfoDetailedReceived);
            Closed += (s, e) => _server.UnregisterSystemInfoHandler(_clientId);

            LoadData();
        }

        private void LoadData()
        {
            StatusText.Text = "Querying system hardware and power status...";
            _server.RequestSystemInfoDetailed(_clientId);
        }

        private void OnSystemInfoDetailedReceived(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            Dispatcher.Invoke(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    TxtDeviceType.Text = root.TryGetProperty("device_type", out var dt) ? dt.GetString() : "Unknown";
                    
                    var battPct = root.TryGetProperty("battery_percent", out var bp) ? bp.GetInt32() : -1;
                    var battStatus = root.TryGetProperty("battery_status", out var bs) ? bs.GetString() : "Unknown";
                    var powerLine = root.TryGetProperty("power_line", out var pl) ? pl.GetString() : "Unknown";

                    TxtBatteryLevel.Text = battPct >= 0 ? $"{battPct}% ({battStatus})" : "No Battery Present (Desktop / AC Only)";
                    TxtPowerStatus.Text = powerLine ?? "Unknown";

                    TxtHost.Text = root.TryGetProperty("machine_name", out var mn) ? mn.GetString() : "";
                    var user = root.TryGetProperty("user_name", out var un) ? un.GetString() : "";
                    var domain = root.TryGetProperty("domain_name", out var dn) ? dn.GetString() : "";
                    TxtUser.Text = string.IsNullOrEmpty(domain) ? user : $"{domain}\\{user}";

                    var os = root.TryGetProperty("os_version", out var ov) ? ov.GetString() : "";
                    var is64 = root.TryGetProperty("is_64bit", out var b64) && b64.GetBoolean() ? "64-bit" : "32-bit";
                    TxtOS.Text = $"{os} ({is64})";

                    var uptimeSec = root.TryGetProperty("uptime_seconds", out var us) ? us.GetInt64() : 0;
                    var ts = TimeSpan.FromSeconds(uptimeSec);
                    TxtUptime.Text = $"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s";

                    var cpu = root.TryGetProperty("cpu_name", out var cn) ? cn.GetString() : "";
                    var cores = root.TryGetProperty("processor_count", out var pc) ? pc.GetInt32() : 0;
                    TxtCPU.Text = $"{cpu} ({cores} logical cores)";

                    var biosVendor = root.TryGetProperty("bios_vendor", out var bv) ? bv.GetString() : "";
                    var biosVer = root.TryGetProperty("bios_version", out var bver) ? bver.GetString() : "";
                    var biosDate = root.TryGetProperty("bios_date", out var bd) ? bd.GetString() : "";
                    TxtBIOS.Text = $"{biosVendor} {biosVer} ({biosDate})".Trim();

                    var ramMb = root.TryGetProperty("total_ram_mb", out var tr) ? tr.GetInt64() : 0;
                    TxtRAM.Text = ramMb > 0 ? $"{ramMb:N0} MB (~{(ramMb / 1024.0):F1} GB)" : "Unknown";

                    var sw = root.TryGetProperty("screen_width", out var w) ? w.GetInt32() : 0;
                    var sh = root.TryGetProperty("screen_height", out var h) ? h.GetInt32() : 0;
                    TxtScreen.Text = sw > 0 ? $"{sw} x {sh}" : "Unknown";

                    var drives = new List<DriveItem>();
                    if (root.TryGetProperty("drives", out var drivesEl) && drivesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in drivesEl.EnumerateArray())
                        {
                            var dName = d.TryGetProperty("name", out var dnEl) ? dnEl.GetString() ?? "" : "";
                            var dFmt = d.TryGetProperty("format", out var dfEl) ? dfEl.GetString() ?? "" : "";
                            var dTot = d.TryGetProperty("total_gb", out var dtEl) ? dtEl.GetDouble() : 0.0;
                            var dFree = d.TryGetProperty("free_gb", out var dfrEl) ? dfrEl.GetDouble() : 0.0;

                            drives.Add(new DriveItem
                            {
                                Name = dName,
                                Format = dFmt,
                                Total = $"{dTot:F1} GB",
                                Free = $"{dFree:F1} GB"
                            });
                        }
                    }
                    DrivesGrid.ItemsSource = drives;

                    StatusText.Text = "System details up to date.";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error parsing system details: {ex.Message}";
                }
            });
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
