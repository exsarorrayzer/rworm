using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public record ProcessEntry(int Pid, string Name, long Mem, string Title);

    public partial class ProcessManagerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private readonly string           _host;
        private List<ProcessEntry>        _all = new();

        public ProcessManagerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            _host   = hostname;
            TitleLabel.Text = $"Processes — {hostname}";
            Refresh();
        }

        private void Refresh_Click(object s, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            StatusLabel.Text = "Refreshing...";
            KillBtn.IsEnabled = false;
            _server.RequestProcessList(_guid, OnProcessList);
        }

        private void OnProcessList(byte[] data)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<JsonElement>>(data);
                if (list == null) return;
                var entries = list.Select(e => new ProcessEntry(
                    e.GetProperty("pid").GetInt32(),
                    e.GetProperty("name").GetString() ?? "",
                    e.GetProperty("mem").GetInt64(),
                    e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : ""
                )).OrderBy(p => p.Name).ToList();

                Dispatcher.Invoke(() =>
                {
                    _all = entries;
                    ApplyFilter(SearchBox.Text);
                    StatusLabel.Text = $"{entries.Count} processes";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusLabel.Text = $"Error: {ex.Message}");
            }
        }

        private void Kill_Click(object s, RoutedEventArgs e)
        {
            if (ProcGrid.SelectedItem is not ProcessEntry p) return;
            if (MessageBox.Show($"Kill {p.Name} (PID {p.Pid})?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _ = _server.KillProcess(_guid, p.Pid);
            StatusLabel.Text = $"Kill sent → PID {p.Pid}";
            Refresh();
        }

        private void ProcGrid_SelectionChanged(object s, SelectionChangedEventArgs e)
            => KillBtn.IsEnabled = ProcGrid.SelectedItem is ProcessEntry;

        private void SearchBox_TextChanged(object s, TextChangedEventArgs e)
            => ApplyFilter(SearchBox.Text);

        private void ApplyFilter(string q)
        {
            var filtered = string.IsNullOrWhiteSpace(q)
                ? _all
                : _all.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || p.Pid.ToString().Contains(q)).ToList();
            ProcGrid.ItemsSource = filtered;
        }
    }
}
