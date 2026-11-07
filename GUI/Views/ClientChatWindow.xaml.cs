using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class ClientChatWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string _guid;
        private readonly string _hostname;

        public ClientChatWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid = guid;
            _hostname = hostname;

            Title = $"Client Chat (v1) — {hostname}";
            TitleLabel.Text = $"Chat — {hostname}";

            _server.RegisterChatRecvHandler(_guid, OnChatMessageReceived);
            Closed += (s, e) =>
            {
                _server.UnregisterChatRecvHandler(_guid);
                _ = _server.CloseClientChat(_guid);
            };

            // Initial greeting notification
            AddSystemNotice($"Chat session initialized with {hostname}. Messages sent appear in client dialogue.");
        }

        private void OnChatMessageReceived(byte[] data)
        {
            var text = Encoding.UTF8.GetString(data);
            Dispatcher.Invoke(() =>
            {
                AddMessageBubble(_hostname, text, isMe: false);
                StatusLabel.Text = $"Last reply received at {DateTime.Now:HH:mm:ss}";
            });
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            DoSendMessage();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                DoSendMessage();
            }
        }

        private void DoSendMessage()
        {
            var text = TxtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            TxtInput.Clear();
            AddMessageBubble("Admin", text, isMe: true);
            _ = _server.SendChatMessage(_guid, text);
            StatusLabel.Text = $"Message sent at {DateTime.Now:HH:mm:ss}";
        }

        private void AddMessageBubble(string sender, string text, bool isMe)
        {
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 4),
                MaxWidth = 480,
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = isMe ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#262626")),
                BorderBrush = isMe ? null : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")),
                BorderThickness = isMe ? new Thickness(0) : new Thickness(1)
            };

            var stack = new StackPanel();

            var senderLabel = new TextBlock
            {
                Text = $"{sender}  •  {DateTime.Now:HH:mm}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = isMe ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A8A")),
                Margin = new Thickness(0, 0, 0, 3)
            };

            var body = new TextBlock
            {
                Text = text,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isMe ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };

            stack.Children.Add(senderLabel);
            stack.Children.Add(body);
            bubble.Child = stack;

            MessagesPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void AddSystemNotice(string notice)
        {
            var textBlock = new TextBlock
            {
                Text = notice,
                FontSize = 11.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A8A")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 6),
                FontStyle = FontStyles.Italic
            };
            MessagesPanel.Children.Add(textBlock);
        }

        private void EndChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
