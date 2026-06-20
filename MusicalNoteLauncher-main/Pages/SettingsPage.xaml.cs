using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            ResetCategoryButtons();
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));

            string content = button.Content.ToString();
            if (content == "常规设置") { tabSettings.SelectedIndex = 0; return; }
            if (content == "游戏设置") { tabSettings.SelectedIndex = 1; return; }
            if (content == "Java设置") { tabSettings.SelectedIndex = 2; return; }
            if (content == "下载设置") { tabSettings.SelectedIndex = 3; return; }
            if (content == "关于") { tabSettings.SelectedIndex = 4; return; }
        }

        private void ResetCategoryButtons()
        {
            btnCategoryGeneral.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            btnCategoryGame.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            btnCategoryJava.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            btnCategoryDownload.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            btnCategoryAbout.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
        }

        private void BtnBrowseJavaPath_Click(object sender, RoutedEventArgs e) { }
        private void BtnDownloadJava_Click(object sender, RoutedEventArgs e) { }
        private void BtnBrowseGamePath_Click(object sender, RoutedEventArgs e) { }
        private void BtnBrowseDownloadPath_Click(object sender, RoutedEventArgs e) { }
        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e) { }
        private void BtnSetJavaPath_Click(object sender, RoutedEventArgs e) { }
        private void BtnDownloadRecommended_Click(object sender, RoutedEventArgs e) { }
        private void BtnValidateJava_Click(object sender, RoutedEventArgs e) { }
        private void BtnDetectJava_Click(object sender, RoutedEventArgs e) { }
        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e) { }
        private void SliderDownloadThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
    }
}
