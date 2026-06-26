using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
		public partial class ComponentStorePage : UserControl
	{
		// Token: 0x060001C0 RID: 448 RVA: 0x00007AC9 File Offset: 0x00005CC9
		public ComponentStorePage()
		{
			this.InitializeComponent();
		}

		// Token: 0x060001C1 RID: 449 RVA: 0x00007AD8 File Offset: 0x00005CD8
		private void BtnSearch_Click(object sender, RoutedEventArgs e)
		{
			string text = this.txtSearch.Text.Trim();
			if (string.IsNullOrEmpty(text))
			{
				MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			MessageBox.Show("搜索: " + text + " 功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x060001C2 RID: 450 RVA: 0x00007B30 File Offset: 0x00005D30
		private void BtnCategory_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				this.ResetCategoryButtons();
				button.Background = (Brush)FindResource("PrimaryBrush");
				MessageBox.Show(string.Format("切换到分类: {0} 功能开发中...", button.Content), "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}

		// Token: 0x060001C3 RID: 451 RVA: 0x00007B88 File Offset: 0x00005D88
		private void ResetCategoryButtons()
		{
			this.btnCategoryAll.Background = (Brush)FindResource("SurfaceBrush");
			this.btnCategoryMods.Background = (Brush)FindResource("SurfaceBrush");
			this.btnCategoryPacks.Background = (Brush)FindResource("SurfaceBrush");
			this.btnCategoryShaders.Background = (Brush)FindResource("SurfaceBrush");
			this.btnCategoryTextures.Background = (Brush)FindResource("SurfaceBrush");
			this.btnCategoryMaps.Background = (Brush)FindResource("SurfaceBrush");
		}

		// Token: 0x060001C4 RID: 452 RVA: 0x00007C4F File Offset: 0x00005E4F
		private void BtnDownload_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("组件下载功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x060001C7 RID: 455 RVA: 0x00007DE3 File Offset: 0x00005FE3
		
	}
}




