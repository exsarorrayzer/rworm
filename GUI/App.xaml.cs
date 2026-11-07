using System;
using System.Windows;

namespace RWORM.GUI
{
    public partial class App : Application
    {
        /// <summary>Wired up in App.xaml: darkens the caption bar of every window.</summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            DarkTitleBar.Apply(sender, e);
        }
    }
}
