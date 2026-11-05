using System.Windows;

namespace RWORM.GUI.Views
{
    public partial class MessageBoxDialog : Window
    {
        public string MsgTitle { get; private set; } = "System Alert";
        public string MsgBody { get; private set; } = "";
        public uint ButtonsVal { get; private set; } = 0;
        public uint IconVal { get; private set; } = 0x40; // MB_ICONINFORMATION

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
                0 => 0, // OK
                1 => 1, // OK / Cancel
                2 => 2, // Abort / Retry / Ignore
                3 => 3, // Yes / No / Cancel
                4 => 4, // Yes / No
                5 => 5, // Retry / Cancel
                _ => 0
            };

            IconVal = (uint)CmbIcon.SelectedIndex switch
            {
                0 => 0x40, // Asterisk / Information
                1 => 0x30, // Exclamation / Warning
                2 => 0x10, // Hand / Stop / Error
                3 => 0x20, // Question
                4 => 0x00, // None
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
