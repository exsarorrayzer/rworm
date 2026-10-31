using System.Windows;
using System.Windows.Controls;

namespace RWORM.GUI.Views
{
    /// <summary>Lightweight single-line input dialog (replaces Microsoft.VisualBasic.Interaction.InputBox).</summary>
    public static class InputDialog
    {
        public static string? Show(string prompt, string title = "Input", string defaultValue = "")
        {
            var win = new Window
            {
                Title  = title,
                Width  = 360, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x22)),
                Foreground = System.Windows.Media.Brushes.White,
            };

            var sp = new StackPanel { Margin = new Thickness(16) };

            var lbl = new TextBlock
            {
                Text       = prompt,
                Foreground = System.Windows.Media.Brushes.White,
                Margin     = new Thickness(0, 0, 0, 8),
            };

            var txt = new TextBox
            {
                Text       = defaultValue,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x21, 0x26, 0x2D)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x30, 0x36, 0x3D)),
                Padding = new Thickness(8, 5, 8, 5),
                Margin  = new Thickness(0, 0, 0, 12),
            };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
                                          HorizontalAlignment = HorizontalAlignment.Right };
            var ok     = new Button { Content = "OK",     Width = 70, Margin = new Thickness(0, 0, 8, 0),
                                      IsDefault = true };
            var cancel = new Button { Content = "Cancel", Width = 70, IsCancel = true };

            string? result = null;
            ok.Click     += (s, e) => { result = txt.Text; win.Close(); };
            cancel.Click += (s, e) => win.Close();

            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);
            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnRow);
            win.Content = sp;

            win.ShowDialog();
            return result;
        }
    }
}
