using System.Windows;
using System.Windows.Controls;

namespace RWORM.GUI.Views
{
    /// <summary>Lightweight input dialog for commands, URLs, and file paths.</summary>
    public static class InputDialog
    {
        public static string? Show(string prompt, string title = "Input", string defaultValue = "")
        {
            var win = new Window
            {
                Title  = title,
                Width  = 640, Height = 250,
                MinWidth = 520, MinHeight = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x22)),
                Foreground = System.Windows.Media.Brushes.White,
            };

            var sp = new Grid { Margin = new Thickness(24) };
            sp.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sp.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sp.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sp.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text       = prompt,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetRow(lbl, 0);

            var txt = new TextBox
            {
                Text       = defaultValue,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x21, 0x26, 0x2D)),
                Foreground = System.Windows.Media.Brushes.White,
                CaretBrush = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x38, 0x44, 0x4D)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(14, 10, 14, 10),
                FontSize = 15,
                FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Segoe UI"),
                Margin  = new Thickness(0, 0, 0, 16),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(txt, 1);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnRow, 3);

            var ok = new Button
            {
                Content = "Run / Send",
                Height = 36,
                Padding = new Thickness(24, 0, 24, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x23, 0x86, 0x36)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            var cancel = new Button
            {
                Content = "Cancel",
                Height = 36,
                Padding = new Thickness(18, 0, 18, 0),
                FontSize = 13,
                IsCancel = true,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x30, 0x36, 0x3D)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            string? result = null;
            ok.Click     += (s, e) => { result = txt.Text; win.Close(); };
            cancel.Click += (s, e) => win.Close();

            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);
            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnRow);
            win.Content = sp;

            win.Loaded += (s, e) => { txt.Focus(); txt.SelectAll(); };

            win.ShowDialog();
            return result;
        }
    }
}
