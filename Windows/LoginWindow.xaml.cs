using System.Windows;
using MusicalNoteLauncher.Pages;

namespace MusicalNoteLauncher.Windows
{
    public partial class LoginWindow : Window
    {
        public string Username { get; private set; }
        public bool IsOfflineMode { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnOfflineLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("请输入用户名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Username = username;
            IsOfflineMode = true;

            MainWindow mainWindow = new MainWindow(Username, IsOfflineMode);
            mainWindow.Show();
            this.Close();
        }

        private void BtnMicrosoftLogin_Click(object sender, RoutedEventArgs e)
        {
            Username = "MicrosoftUser";
            IsOfflineMode = false;

            MessageBox.Show("微软正版登录功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            MainWindow mainWindow = new MainWindow(Username, IsOfflineMode);
            mainWindow.Show();
            this.Close();
        }
    }
}