using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
		public partial class JavaConfigPage : UserControl
	{
								public event Action<int> JavaDownloadRequested;

		public JavaConfigPage() : this(AppContext.MinecraftPath) { }

		// Token: 0x06000289 RID: 649 RVA: 0x0000C7D0 File Offset: 0x0000A9D0
		public JavaConfigPage(string minecraftPath)
		{
			this.InitializeComponent();
			this._minecraftPath = minecraftPath;
			this._javaConfig = new JavaConfigManager(minecraftPath);
			this._javaDownloadService = new JavaDownloadService(minecraftPath);
			this._javaConfig.StatusChanged += this.OnStatusChanged;
			this._javaConfig.LogReceived += this.OnLogReceived;
			this.UpdateCurrentConfig();
			this.UpdateVersionSuggestion();
			this.UpdateRecommendedJava();
		}

		// Token: 0x0600028A RID: 650 RVA: 0x0000C850 File Offset: 0x0000AA50
		private void UpdateRecommendedJava()
		{
			string selectedMinecraftVersion = this._javaConfig.GetSelectedMinecraftVersion();
			int recommendedJavaVersion;
			if (!string.IsNullOrEmpty(selectedMinecraftVersion) && int.TryParse(this._javaDownloadService.GetRecommendedJavaVersion(selectedMinecraftVersion), out recommendedJavaVersion))
			{
				this._recommendedJavaVersion = recommendedJavaVersion;
			}
			this.txtRecommendedJava.Text = string.Format("Java {0}", this._recommendedJavaVersion);
		}

		// Token: 0x0600028B RID: 651 RVA: 0x0000C8B0 File Offset: 0x0000AAB0
		public void SetMinecraftVersion(string version)
		{
			int num;
			if (!string.IsNullOrEmpty(version) && int.TryParse(this._javaDownloadService.GetRecommendedJavaVersion(version), out num))
			{
				this._recommendedJavaVersion = num;
				this.txtRecommendedJava.Text = string.Format("Java {0}", num);
				this.OnLogReceived(string.Format("MC版本 {0} 推荐使用 Java {1}", version, num));
			}
		}

		// Token: 0x0600028C RID: 652 RVA: 0x0000C914 File Offset: 0x0000AB14
		private void OnStatusChanged(string status)
		{
			base.Dispatcher.Invoke(delegate()
			{
				this.OnLogReceived("[状态] " + status);
			});
		}

		// Token: 0x0600028D RID: 653 RVA: 0x0000C94C File Offset: 0x0000AB4C
		private void OnLogReceived(string log)
		{
			base.Dispatcher.Invoke(delegate()
			{
				if (!string.IsNullOrEmpty(this.txtLog.Text))
				{
					TextBox textBox = this.txtLog;
					textBox.Text += Environment.NewLine;
				}
				TextBox textBox2 = this.txtLog;
				textBox2.Text += string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, log);
				this.txtLog.ScrollToEnd();
			});
		}

		// Token: 0x0600028E RID: 654 RVA: 0x0000C984 File Offset: 0x0000AB84
		private void UpdateCurrentConfig()
		{
			this.txtCurrentJavaPath.Text = this._javaConfig.GetJavaPath();
			this.txtCurrentJavaVersion.Text = string.Format("Java {0}", this._javaConfig.GetJavaVersion());
			TextBlock textBlock = this.txtConfigType;
			JavaConfigManager.JavaConfig currentConfig = this._javaConfig.CurrentConfig;
			textBlock.Text = ((currentConfig != null && currentConfig.IsAutoConfigured) ? "自动配置" : "手动配置");
			this.txtRecommendedMemory.Text = string.Format("{0} MB", this._javaConfig.GetMaxMemoryMb());
		}

		// Token: 0x0600028F RID: 655 RVA: 0x0000CA21 File Offset: 0x0000AC21
		private void UpdateVersionSuggestion()
		{
			this.txtVersionSuggestion.Text = "• Minecraft 1.17+ (含1.18, 1.19, 1.20等): 建议使用 Java 17\n• Minecraft 1.9 ~ 1.16: 建议使用 Java 11\n• Minecraft 1.8 及以下: 建议使用 Java 8";
		}

		// Token: 0x06000290 RID: 656 RVA: 0x0000CA34 File Offset: 0x0000AC34
		private void btnDetectJava_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this._detectedJavaList = this._javaConfig.DetectInstalledJava();
				this.javaList.ItemsSource = this._detectedJavaList;
				if (this._detectedJavaList.Count == 0)
				{
					MessageBox.Show("未检测到系统中的Java环境，请手动配置或下载Java", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					this.OnLogReceived(string.Format("共检测到 {0} 个Java环境", this._detectedJavaList.Count));
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("检测Java失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000291 RID: 657 RVA: 0x0000CAD8 File Offset: 0x0000ACD8
		private void btnUseJava_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				JavaConfigManager.DetectedJava detectedJava = button.Tag as JavaConfigManager.DetectedJava;
				if (detectedJava != null)
				{
					try
					{
						this._javaConfig.SetAutoConfig(detectedJava);
						this.UpdateCurrentConfig();
						MessageBox.Show(string.Format("已使用 Java {0}\n路径: {1}", detectedJava.MajorVersion, detectedJava.Path), "配置成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					}
					catch (Exception ex)
					{
						MessageBox.Show("设置Java失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
					}
				}
			}
		}

		// Token: 0x06000292 RID: 658 RVA: 0x0000CB6C File Offset: 0x0000AD6C
		private void btnBrowseJava_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Java可执行文件 (java.exe)|java.exe|All files (*.*)|*.*",
				Title = "选择 Java 可执行文件",
				FileName = "java.exe"
			};
			if (openFileDialog.ShowDialog().GetValueOrDefault())
			{
				this.txtJavaPath.Text = openFileDialog.FileName;
			}
		}

		// Token: 0x06000293 RID: 659 RVA: 0x0000CBC4 File Offset: 0x0000ADC4
		private void btnSetJavaPath_Click(object sender, RoutedEventArgs e)
		{
			string text = this.txtJavaPath.Text.Trim();
			if (string.IsNullOrEmpty(text))
			{
				MessageBox.Show("请输入Java路径或浏览选择", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			try
			{
				if (!this._javaConfig.ValidateJavaPath(text))
				{
					MessageBox.Show("无效的Java路径或Java版本无法识别", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				else
				{
					this._javaConfig.SetJavaPath(text);
					this.UpdateCurrentConfig();
					MessageBox.Show("已设置Java路径: " + text, "配置成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("设置Java失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000294 RID: 660 RVA: 0x0000CC80 File Offset: 0x0000AE80
		private void btnValidateJava_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string javaPath = this._javaConfig.GetJavaPath();
				this.OnLogReceived("正在验证Java: " + javaPath);
				if (this._javaConfig.ValidateJavaPath(javaPath))
				{
					this.OnLogReceived("Java配置验证通过!");
					MessageBox.Show("Java配置验证通过!", "验证成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					this.OnLogReceived("Java配置验证失败!");
					MessageBox.Show("Java配置验证失败，请检查路径是否正确", "验证失败", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("验证失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000295 RID: 661 RVA: 0x0000CD2C File Offset: 0x0000AF2C
		private void btnDownloadJava_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				string text = button.Tag as string;
				int javaVersion;
				if (text != null && int.TryParse(text, out javaVersion))
				{
					this.AddJavaDownloadTask(javaVersion);
				}
			}
		}

		// Token: 0x06000296 RID: 662 RVA: 0x0000CD63 File Offset: 0x0000AF63
		private void btnDownloadRecommended_Click(object sender, RoutedEventArgs e)
		{
			this.AddJavaDownloadTask(this._recommendedJavaVersion);
		}

		// Token: 0x06000297 RID: 663 RVA: 0x0000CD74 File Offset: 0x0000AF74
		private void AddJavaDownloadTask(int javaVersion)
		{
			try
			{
				if (this._javaDownloadService.IsJavaInstalled(javaVersion))
				{
					string installedJavaPath = this._javaDownloadService.GetInstalledJavaPath(javaVersion);
					if (MessageBox.Show(string.Format("Java {0} 已安装!\n路径: {1}\n\n是否使用此版本?", javaVersion, installedJavaPath), "已安装", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
					{
						this.txtJavaPath.Text = installedJavaPath;
						this._javaConfig.SetJavaPath(installedJavaPath);
						this.UpdateCurrentConfig();
					}
				}
				else
				{
					this.OnLogReceived(string.Format("将 Java {0} 下载任务添加到下载列表...", javaVersion));
					Action<int> javaDownloadRequested = this.JavaDownloadRequested;
					if (javaDownloadRequested != null)
					{
						javaDownloadRequested(javaVersion);
					}
					MessageBox.Show(string.Format("Java {0} 下载任务已添加到下载列表!\n\n请切换到「下载任务」页面查看下载进度。", javaVersion), "任务已添加", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			catch (Exception ex)
			{
				this.OnLogReceived("添加下载任务失败: " + ex.Message);
				MessageBox.Show("添加下载任务失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600029A RID: 666 RVA: 0x0000D0FA File Offset: 0x0000B2FA
		

				private readonly JavaConfigManager _javaConfig;

				private readonly string _minecraftPath;

				private List<JavaConfigManager.DetectedJava> _detectedJavaList;

				private JavaDownloadService _javaDownloadService;

				private int _recommendedJavaVersion = 8;
	}
}




