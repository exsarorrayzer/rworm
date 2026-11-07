using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace RWORM.GUI.Views
{
    public partial class CreditsWindow : Window
    {
        public CreditsWindow()
        {
            InitializeComponent();
            string localizedTitle = GetLocalizedLeaderTitle();
            LeaderTitleTooltip.Text = localizedTitle;
            RayzerLabel.ToolTip = localizedTitle;
        }

        public static string GetLocalizedLeaderTitle()
        {
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return lang switch
            {
                "tr" => "Ulu Bilge Lider",
                "en" => "Supreme Wise Leader",
                "ru" => "Великий мудрый вождь",
                "de" => "Großer weiser Führer",
                "fr" => "Grand guide sage",
                "es" => "Gran líder sabio",
                "it" => "Grande saggio leader",
                "pt" => "Grande líder sábio",
                "zh" => "至高无上的智慧领袖",
                "ja" => "至高の賢明な指導者",
                "ko" => "위대한 지혜로운 지도자",
                "ar" => "القائد الحكيم الأعظم",
                _ => "Supreme Wise Leader / Ulu Bilge Lider"
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
