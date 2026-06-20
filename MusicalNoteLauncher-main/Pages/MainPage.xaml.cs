using System;
using System.Windows.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class MainPage : UserControl
    {
        public MainPage()
        {
            InitializeComponent();
        }
        private void BtnDownloadVersion_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnSaveSettings_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnLogout_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnAutoDetectJava_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnLaunch_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnBrowseJava_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnRefreshVersions_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void BtnSelectGameDir_Click(object sender, System.Windows.RoutedEventArgs e) { }
        private void CmbGameVersion_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }
        private void SliderMemory_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) { }
    }
}
