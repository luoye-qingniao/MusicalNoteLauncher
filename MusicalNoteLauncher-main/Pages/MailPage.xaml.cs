using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MusicalNoteLauncher.Pages
{
		public partial class MailPage : UserControl
	{
		// Token: 0x060002B8 RID: 696 RVA: 0x0000DF81 File Offset: 0x0000C181
		public MailPage()
		{
			this.InitializeComponent();
		}

		// Token: 0x060002B9 RID: 697 RVA: 0x0000DF8F File Offset: 0x0000C18F
		private void BtnReply_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("回复邮件功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x060002BA RID: 698 RVA: 0x0000DFA4 File Offset: 0x0000C1A4
		private void BtnDelete_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("删除邮件功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}
}



