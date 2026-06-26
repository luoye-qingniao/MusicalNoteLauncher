using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
		public partial class SaveManagerPage : UserControl
	{
				// (get) Token: 0x06000352 RID: 850 RVA: 0x0001220B File Offset: 0x0001040B
		private string SavesPath
		{
			get
			{
				return Path.Combine(this.ExpandEnvironmentVariables(SettingsManager.Settings.GamePath), "saves");
			}
		}

				// (get) Token: 0x06000353 RID: 851 RVA: 0x00012227 File Offset: 0x00010427
		private string BackupsPath
		{
			get
			{
				return Path.Combine(this.ExpandEnvironmentVariables(SettingsManager.Settings.GamePath), "backups");
			}
		}

		// Token: 0x06000354 RID: 852 RVA: 0x00012244 File Offset: 0x00010444
		private string ExpandEnvironmentVariables(string path)
		{
			string result;
			try
			{
				result = Environment.ExpandEnvironmentVariables(path);
			}
			catch
			{
				result = path;
			}
			return result;
		}

		// Token: 0x06000355 RID: 853 RVA: 0x00012270 File Offset: 0x00010470
		public SaveManagerPage()
		{
			this.InitializeComponent();
			this.LoadSaves();
		}

		// Token: 0x06000356 RID: 854 RVA: 0x00012284 File Offset: 0x00010484
		private void LoadSaves()
		{
			this.lstSaves.Items.Clear();
			string path = this.ExpandEnvironmentVariables(SettingsManager.Settings.GamePath);

			// 1. 先加载各版本的独立存档（PCL风格：按版本隔离级别逐版本判断）
			string versionsDir = Path.Combine(path, "versions");
			if (Directory.Exists(versionsDir))
			{
				foreach (string verDir in Directory.GetDirectories(versionsDir))
				{
					string versionId = Path.GetFileName(verDir);
					// 检查该版本是否应被隔离
					if (SettingsManager.Settings.ShouldIsolateVersionForVersion(path, versionId))
					{
						string versionSavesDir = Path.Combine(verDir, "game", "saves");
						if (Directory.Exists(versionSavesDir))
						{
							foreach (string saveDir in Directory.GetDirectories(versionSavesDir))
							{
								string saveName = Path.GetFileName(saveDir);
								long directorySize = this.GetDirectorySize(saveDir);
								string playTime = this.GetPlayTime(saveDir);
								this.lstSaves.Items.Add(new SaveManagerPage.SaveItem
								{
									SaveName = saveName + " (" + versionId + ")",
									SaveInfo = this.FormatSize(directorySize),
									PlayTime = playTime,
									FolderPath = saveDir,
									FileSize = directorySize
								});
							}
						}
					}
				}
			}

			// 2. 再加载全局存档（非隔离版本的存档在全局目录）
			//    如果全部版本都隔离了（设为"隔离所有版本"），全局目录可能无存档
			string globalSavesDir = Path.Combine(path, "saves");
			if (Directory.Exists(globalSavesDir))
			{
				foreach (string saveDir in Directory.GetDirectories(globalSavesDir))
				{
					string saveName = Path.GetFileName(saveDir);
					long directorySize = this.GetDirectorySize(saveDir);
					string playTime = this.GetPlayTime(saveDir);
					this.lstSaves.Items.Add(new SaveManagerPage.SaveItem
					{
						SaveName = saveName,
						SaveInfo = this.FormatSize(directorySize),
						PlayTime = playTime,
						FolderPath = saveDir,
						FileSize = directorySize
					});
				}
			}

			// 如果没有任何存档，创建全局存档目录
			if (this.lstSaves.Items.Count == 0)
			{
				if (!Directory.Exists(globalSavesDir))
					Directory.CreateDirectory(globalSavesDir);
			}
		}

		// Token: 0x06000357 RID: 855 RVA: 0x00012450 File Offset: 0x00010650
		private long GetDirectorySize(string path)
		{
			long result;
			try
			{
				result = new DirectoryInfo(path).EnumerateFiles("*.*", SearchOption.AllDirectories).Sum((FileInfo f) => f.Length);
			}
			catch
			{
				result = 0L;
			}
			return result;
		}

		// Token: 0x06000358 RID: 856 RVA: 0x000124AC File Offset: 0x000106AC
		private string FormatSize(long bytes)
		{
			if (bytes < 1024L)
			{
				return bytes.ToString() + " B";
			}
			if (bytes < 1048576L)
			{
				return ((double)bytes / 1024.0).ToString("F2") + " KB";
			}
			if (bytes < 1073741824L)
			{
				return ((double)bytes / 1048576.0).ToString("F2") + " MB";
			}
			return ((double)bytes / 1073741824.0).ToString("F2") + " GB";
		}

		// Token: 0x06000359 RID: 857 RVA: 0x00012554 File Offset: 0x00010754
		private string GetPlayTime(string folder)
		{
			try
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(folder);
				if (directoryInfo.Exists)
				{
					return directoryInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
				}
			}
			catch
			{
				return "未知时间";
			}
			return "未知时间";
		}

		// Token: 0x0600035A RID: 858 RVA: 0x000125A8 File Offset: 0x000107A8
		private void BtnLaunchSave_Click(object sender, RoutedEventArgs e)
		{
			SaveManagerPage.SaveItem saveItem = this.lstSaves.SelectedItem as SaveManagerPage.SaveItem;
			if (saveItem != null)
			{
				MessageBox.Show("准备启动存档: " + saveItem.SaveName, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			MessageBox.Show("请先选择一个存档", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}

		// Token: 0x0600035B RID: 859 RVA: 0x000125FC File Offset: 0x000107FC
		private void BtnBackupSave_Click(object sender, RoutedEventArgs e)
		{
			SaveManagerPage.SaveItem saveItem = this.lstSaves.SelectedItem as SaveManagerPage.SaveItem;
			if (saveItem != null)
			{
				try
				{
					if (!Directory.Exists(this.BackupsPath))
					{
						Directory.CreateDirectory(this.BackupsPath);
					}
					string path = string.Format("{0}_{1:yyyyMMdd_HHmmss}.zip", saveItem.SaveName, DateTime.Now);
					string text = Path.Combine(this.BackupsPath, path);
					ZipFile.CreateFromDirectory(saveItem.FolderPath, text);
					MessageBox.Show("存档备份成功！\n备份位置: " + text, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					return;
				}
				catch (Exception ex)
				{
					MessageBox.Show("备份失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
					return;
				}
			}
			MessageBox.Show("请先选择一个存档", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}

		// Token: 0x0600035C RID: 860 RVA: 0x000126CC File Offset: 0x000108CC
		private void BtnRenameSave_Click(object sender, RoutedEventArgs e)
		{
			SaveManagerPage.SaveItem saveItem = this.lstSaves.SelectedItem as SaveManagerPage.SaveItem;
			if (saveItem != null)
			{
				InputBox inputBox = new InputBox("重命名存档", "请输入新的存档名称:", saveItem.SaveName);
				if (inputBox.ShowDialog().GetValueOrDefault() && !string.IsNullOrWhiteSpace(inputBox.ResponseText))
				{
					string path = inputBox.ResponseText.Trim();
					string text = Path.Combine(this.SavesPath, path);
					if (!Directory.Exists(text))
					{
						Directory.Move(saveItem.FolderPath, text);
						this.LoadSaves();
						MessageBox.Show("存档重命名成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						return;
					}
					MessageBox.Show("存档名称已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
			}
			else
			{
				MessageBox.Show("请先选择一个存档", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}

		// Token: 0x0600035D RID: 861 RVA: 0x00012794 File Offset: 0x00010994
		private void BtnDeleteSave_Click(object sender, RoutedEventArgs e)
		{
			SaveManagerPage.SaveItem saveItem = this.lstSaves.SelectedItem as SaveManagerPage.SaveItem;
			if (saveItem != null)
			{
				if (MessageBox.Show("确定要删除存档 \"" + saveItem.SaveName + "\" 吗？此操作不可撤销！", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
				{
					return;
				}
				try
				{
					Directory.Delete(saveItem.FolderPath, true);
					this.LoadSaves();
					MessageBox.Show("存档删除成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					return;
				}
				catch (Exception ex)
				{
					MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
					return;
				}
			}
			MessageBox.Show("请先选择一个存档", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}

		// Token: 0x0600035E RID: 862 RVA: 0x00012848 File Offset: 0x00010A48
		private void BtnRefreshSaves_Click(object sender, RoutedEventArgs e)
		{
			this.LoadSaves();
			MessageBox.Show("存档列表已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x0600035F RID: 863 RVA: 0x00012863 File Offset: 0x00010A63
		private void BtnBackupManager_Click(object sender, RoutedEventArgs e)
		{
			if (this.lstSaves.SelectedItem is SaveManagerPage.SaveItem)
			{
				this.BtnBackupSave_Click(sender, e);
				return;
			}
			MessageBox.Show("备份管理\n\n已备份存档位置: " + this.BackupsPath, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}

		// Token: 0x06000360 RID: 864 RVA: 0x000128A0 File Offset: 0x00010AA0
		private void BtnImportSave_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "存档压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*",
				Title = "选择要导入的存档压缩文件"
			};
			if (openFileDialog.ShowDialog().GetValueOrDefault())
			{
				string fileName = openFileDialog.FileName;
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
				string text = Path.Combine(this.SavesPath, fileNameWithoutExtension);
				if (!Directory.Exists(text))
				{
					ZipFile.ExtractToDirectory(fileName, text);
					this.LoadSaves();
					MessageBox.Show("存档导入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					return;
				}
				MessageBox.Show("存档名称已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}

				public class SaveItem
		{
						// (get) Token: 0x060007D0 RID: 2000 RVA: 0x000296E2 File Offset: 0x000278E2
			// (set) Token: 0x060007D1 RID: 2001 RVA: 0x000296EA File Offset: 0x000278EA
			public string SaveName { get; set; }

						// (get) Token: 0x060007D2 RID: 2002 RVA: 0x000296F3 File Offset: 0x000278F3
			// (set) Token: 0x060007D3 RID: 2003 RVA: 0x000296FB File Offset: 0x000278FB
			public string SaveInfo { get; set; }

						// (get) Token: 0x060007D4 RID: 2004 RVA: 0x00029704 File Offset: 0x00027904
			// (set) Token: 0x060007D5 RID: 2005 RVA: 0x0002970C File Offset: 0x0002790C
			public string PlayTime { get; set; }

						// (get) Token: 0x060007D6 RID: 2006 RVA: 0x00029715 File Offset: 0x00027915
			// (set) Token: 0x060007D7 RID: 2007 RVA: 0x0002971D File Offset: 0x0002791D
			public string FolderPath { get; set; }

						// (get) Token: 0x060007D8 RID: 2008 RVA: 0x00029726 File Offset: 0x00027926
			// (set) Token: 0x060007D9 RID: 2009 RVA: 0x0002972E File Offset: 0x0002792E
			public long FileSize { get; set; }
		}
	}
}



