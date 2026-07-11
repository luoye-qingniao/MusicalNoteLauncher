using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
		public partial class DownloadTaskPage : UserControl
	{
		// Token: 0x06000200 RID: 512 RVA: 0x00008BB2 File Offset: 0x00006DB2
		public DownloadTaskPage()
		{
			this.InitializeComponent();
			base.Loaded += this.DownloadTaskPage_Loaded;
		}

		// Token: 0x06000201 RID: 513 RVA: 0x00008BD4 File Offset: 0x00006DD4
		private void DownloadTaskPage_Loaded(object sender, RoutedEventArgs e)
		{
			this.lstDownloadTasks.ItemsSource = DownloadTaskManager.Instance.Tasks;
			this.UpdateEmptyState();
			DownloadTaskManager.Instance.TaskAdded += new Action<IDownloadTask>(this.OnTaskAdded);
			DownloadTaskManager.Instance.TaskCompleted += new Action<IDownloadTask>(this.OnTaskCompleted);
			DownloadTaskManager.Instance.TaskFailed += new Action<IDownloadTask>(this.OnTaskFailed);
		}

		// Token: 0x06000202 RID: 514 RVA: 0x00008C3E File Offset: 0x00006E3E
		private void OnTaskAdded(object task)
		{
			base.Dispatcher.Invoke(delegate()
			{
				this.UpdateEmptyState();
			});
		}

		// Token: 0x06000203 RID: 515 RVA: 0x00008C57 File Offset: 0x00006E57
		private void OnTaskCompleted(object task)
		{
			base.Dispatcher.Invoke(delegate()
			{
				this.UpdateEmptyState();
			});
		}

		// Token: 0x06000204 RID: 516 RVA: 0x00008C70 File Offset: 0x00006E70
		private void OnTaskFailed(object task)
		{
			base.Dispatcher.Invoke(delegate()
			{
				this.UpdateEmptyState();
			});
		}

		// Token: 0x06000205 RID: 517 RVA: 0x00008C89 File Offset: 0x00006E89
		private void UpdateEmptyState()
		{
			this.pnlEmptyState.Visibility = (DownloadTaskManager.Instance.HasTasks ? Visibility.Collapsed : Visibility.Visible);
		}

		// Token: 0x06000206 RID: 518 RVA: 0x00008CA6 File Offset: 0x00006EA6
		private void BtnPauseAll_Click(object sender, RoutedEventArgs e)
		{
			DownloadTaskManager.Instance.PauseAllTasks();
			Logger.Info("[下载任务] 用户点击暂停全部");
		}

		// Token: 0x06000207 RID: 519 RVA: 0x00008CBC File Offset: 0x00006EBC
		private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
		{
			DownloadTaskManager.Instance.ClearCompletedTasks();
			this.UpdateEmptyState();
			Logger.Info("[下载任务] 用户点击清空已完成");
		}

		// Token: 0x06000208 RID: 520 RVA: 0x00008CD8 File Offset: 0x00006ED8
		private void BtnPause_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				DownloadTaskViewModel downloadTaskViewModel = button.DataContext as DownloadTaskViewModel;
				if (downloadTaskViewModel != null)
				{
					downloadTaskViewModel.Pause();
					Logger.Info("[下载任务] 用户暂停任务: " + downloadTaskViewModel.VersionId);
				}
			}
		}

		// Token: 0x06000209 RID: 521 RVA: 0x00008D1C File Offset: 0x00006F1C
		private void BtnResume_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				DownloadTaskViewModel downloadTaskViewModel = button.DataContext as DownloadTaskViewModel;
				if (downloadTaskViewModel != null)
				{
					DownloadTaskManager.Instance.ResumeTask(downloadTaskViewModel);
					Logger.Info("[下载任务] 用户恢复任务: " + downloadTaskViewModel.VersionId);
				}
			}
		}

		// Token: 0x0600020A RID: 522 RVA: 0x00008D64 File Offset: 0x00006F64
		private void BtnDelete_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				DownloadTaskViewModel downloadTaskViewModel = button.DataContext as DownloadTaskViewModel;
				if (downloadTaskViewModel != null)
				{
					DownloadTaskManager.Instance.DeleteTask(downloadTaskViewModel);
					this.UpdateEmptyState();
					Logger.Info("[下载任务] 用户删除任务: " + downloadTaskViewModel.VersionId);
				}
			}
		}

		// Token: 0x0600020D RID: 525 RVA: 0x00008E64 File Offset: 0x00007064
		
	}
}




