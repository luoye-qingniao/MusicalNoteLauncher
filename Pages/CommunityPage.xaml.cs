using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MusicalNoteLauncher.Pages
{
		public partial class CommunityPage : UserControl
	{
		// Token: 0x060001BA RID: 442 RVA: 0x000079BB File Offset: 0x00005BBB
		public CommunityPage()
		{
			this.InitializeComponent();
		}

		// Token: 0x060001BB RID: 443 RVA: 0x000079C9 File Offset: 0x00005BC9
		private void BtnPost_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(this.txtPostContent.Text.Trim()))
			{
				MessageBox.Show("请输入帖子内容", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			MessageBox.Show("发布帖子功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x060001BC RID: 444 RVA: 0x00007A09 File Offset: 0x00005C09
		private void BtnReply_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("回复功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x060001BF RID: 447 RVA: 0x00007AAC File Offset: 0x00005CAC
		
	}
}




