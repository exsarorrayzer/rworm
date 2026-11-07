using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RWORM.GUI.ViewModels;

namespace RWORM.GUI.Views
{
    public partial class BuilderWindow : Window
    {
        private readonly MainViewModel _vm;

        public BuilderWindow(MainViewModel vm, Point? launchOrigin = null)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            if (launchOrigin.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Math.Max(20, launchOrigin.Value.X - Width / 2);
                Top = Math.Max(20, launchOrigin.Value.Y - Height / 2);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else
                    DragMove();
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag != null)
            {
                if (int.TryParse(btn.Tag.ToString(), out int idx))
                {
                    TabPages.SelectedIndex = idx;
                }
            }
        }
    }
}
