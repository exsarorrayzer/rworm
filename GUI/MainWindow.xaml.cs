using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RWORM.GUI.Models;
using RWORM.GUI.ViewModels;

namespace RWORM.GUI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
            Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            _vm.KillServer();
        }

        /// Sync DataGrid multi-selection → ViewModel.SelectedClients
        private void ClientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SelectedClients.Clear();
            foreach (ClientModel item in ClientsGrid.SelectedItems.OfType<ClientModel>())
                _vm.SelectedClients.Add(item);
        }
    }
}