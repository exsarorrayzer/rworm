using System;
using System.Windows;
using System.Windows.Controls;
using RWORM.GUI.ViewModels;

namespace RWORM.GUI.Views
{
    public partial class BuilderWindow : Window
    {
        private readonly MainViewModel _vm;

        public BuilderWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
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
