using System.Windows;

namespace RWORM.GUI.Views
{
    public partial class MessageBoxDialog : Window
    {
        public string MsgTitle { get; private set; } = "System Alert";
        public string MsgBody { get; private set; } = "";
        public uint ButtonsVal { get; private set; } = 0;
        public uint IconVal { get; private set; } = 0x40; 

        public MessageBoxDialog()
        {
            InitializeComponent();
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            MsgTitle = TxtTitle.Text.Trim();
            MsgBody = TxtMessage.Text.Trim();

            ButtonsVal = (uint)CmbButtons.SelectedIndex switch
            {
                0 => 0, 
                1 => 1, 
                2 => 2, 
                3 => 3, 
                4 => 4, 
                5 => 5, 
                _ => 0
            };

            IconVal = (uint)CmbIcon.SelectedIndex switch
            {
                0 => 0x40, 
                1 => 0x30, 
                2 => 0x10, 
                3 => 0x20, 
                4 => 0x00, 
                _ => 0x40
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
