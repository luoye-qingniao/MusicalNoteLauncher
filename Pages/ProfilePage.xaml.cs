using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MusicalNoteLauncher.Pages
{
		public partial class ProfilePage : UserControl
	{
		// Token: 0x06000349 RID: 841 RVA: 0x00012065 File Offset: 0x00010265
		public ProfilePage()
		{
			this.InitializeComponent();
		}

		// Token: 0x0600034A RID: 842 RVA: 0x00012073 File Offset: 0x00010273
		public ProfilePage(string username, bool isOfflineMode) : this()
		{
			this.txtUsername.Text = username;
			this.txtLoginMode.Text = (isOfflineMode ? "离线模式" : "正版模式");
		}

		// Token: 0x0600034B RID: 843 RVA: 0x000120A1 File Offset: 0x000102A1
		private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("编辑个人资料功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x0600034C RID: 844 RVA: 0x000120B6 File Offset: 0x000102B6
		private void BtnSwitchAccount_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("切换账号功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x0600034D RID: 845 RVA: 0x000120CB File Offset: 0x000102CB
		private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("快速开始功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x0600034E RID: 846 RVA: 0x000120E0 File Offset: 0x000102E0
		private void BtnBrowseMods_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("浏览模组功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x0600034F RID: 847 RVA: 0x000120F5 File Offset: 0x000102F5
		private void BtnGameSettings_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("游戏设置功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}
}



