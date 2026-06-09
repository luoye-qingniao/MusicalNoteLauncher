using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
    public partial class FriendsListPage : UserControl
    {
        public FriendsListPage()
        {
            this.InitializeComponent();
        }

        private void BtnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("添加好友功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                this.ResetFilterButtons();
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                MessageBox.Show(string.Format("筛选: {0} 功能开发中...", button.Content), "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void ResetFilterButtons()
        {
            this.btnFilterAll.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            this.btnFilterOnline.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            this.btnFilterOffline.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
        }

        private void BtnInvite_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("邀请好友联机功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnChat_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("打开聊天窗口功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this.txtMessage.Text.Trim()))
            {
                MessageBox.Show("请输入消息内容", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            MessageBox.Show("发送消息功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }
    }
}
