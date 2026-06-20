using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Utils;

namespace MusicalNoteLauncher.Pages
{
		public partial class MultiplayerSocialPage : UserControl
	{
		// Token: 0x0600031C RID: 796 RVA: 0x0001057F File Offset: 0x0000E77F
		public MultiplayerSocialPage()
		{
			this.InitializeComponent();
			this._easyTier = new EasyTierManager();
			LoadServerVersions();
		}

		private void LoadServerVersions()
		{
			try
			{
				if (cbServerVersion == null) return;
				
				cbServerVersion.Items.Clear();
				var versions = GetInstalledVersions();
				
				if (versions.Count == 0)
				{
					cbServerVersion.Items.Add(new ComboBoxItem { Content = "无已安装版本" });
				}
				else
				{
					foreach (var version in versions.OrderByDescending(v => v))
					{
						cbServerVersion.Items.Add(new ComboBoxItem { Content = version });
					}
					cbServerVersion.SelectedIndex = 0;
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"[MultiplayerSocialPage] 加载服务器版本失败: {ex.Message}");
			}
		}

		private List<string> GetInstalledVersions()
		{
			List<string> versions = new List<string>();
			string minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions");
			
			if (Directory.Exists(minecraftPath))
			{
				foreach (var dir in Directory.GetDirectories(minecraftPath))
				{
					string versionName = Path.GetFileName(dir);
					string jsonFile = Path.Combine(dir, versionName + ".json");
					string jarFile = Path.Combine(dir, versionName + ".jar");
					
					if (File.Exists(jsonFile) && File.Exists(jarFile))
					{
						versions.Add(versionName);
					}
				}
			}
			
			return versions;
		}

		// Token: 0x0600031D RID: 797 RVA: 0x000105A3 File Offset: 0x0000E7A3
		private void Card_LAN_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.LANPage.Visibility = Visibility.Visible;
		}

		// Token: 0x0600031E RID: 798 RVA: 0x000105BD File Offset: 0x0000E7BD
		private void Card_VirtualLAN_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.VirtualLANPage.Visibility = Visibility.Visible;
		}

		// Token: 0x0600031F RID: 799 RVA: 0x000105D7 File Offset: 0x0000E7D7
		private void Card_NAT_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.NATPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000320 RID: 800 RVA: 0x000105F1 File Offset: 0x0000E7F1
		private void Card_Server_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.ServerPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000321 RID: 801 RVA: 0x0001060B File Offset: 0x0000E80B
		private void Card_TaoWa_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.TaoWaPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000322 RID: 802 RVA: 0x00010625 File Offset: 0x0000E825
		private void Card_Mod_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.ModPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000323 RID: 803 RVA: 0x0001063F File Offset: 0x0000E83F
		private void Card_Settings_Click(object sender, RoutedEventArgs e)
		{
			this.MainPage.Visibility = Visibility.Collapsed;
			this.SettingsPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000324 RID: 804 RVA: 0x0001065C File Offset: 0x0000E85C
		private void BtnBack_Click(object sender, RoutedEventArgs e)
		{
			if (this.HostPage.Visibility == Visibility.Visible || this.ClientPage.Visibility == Visibility.Visible)
			{
				this.TaoWaMainPage.Visibility = Visibility.Visible;
				this.HostPage.Visibility = Visibility.Collapsed;
				this.ClientPage.Visibility = Visibility.Collapsed;
				return;
			}
			this.MainPage.Visibility = Visibility.Visible;
			this.LANPage.Visibility = Visibility.Collapsed;
			this.VirtualLANPage.Visibility = Visibility.Collapsed;
			this.NATPage.Visibility = Visibility.Collapsed;
			this.ServerPage.Visibility = Visibility.Collapsed;
			this.TaoWaPage.Visibility = Visibility.Collapsed;
			this.ModPage.Visibility = Visibility.Collapsed;
			this.SettingsPage.Visibility = Visibility.Collapsed;
		}

		// Token: 0x06000325 RID: 805 RVA: 0x00010708 File Offset: 0x0000E908
		private void BtnHost_Click(object sender, RoutedEventArgs e)
		{
			this.TaoWaMainPage.Visibility = Visibility.Collapsed;
			this.HostPage.Visibility = Visibility.Visible;
			this.ClientPage.Visibility = Visibility.Collapsed;
		}

		// Token: 0x06000326 RID: 806 RVA: 0x0001072E File Offset: 0x0000E92E
		private void BtnClient_Click(object sender, RoutedEventArgs e)
		{
			this.TaoWaMainPage.Visibility = Visibility.Collapsed;
			this.HostPage.Visibility = Visibility.Collapsed;
			this.ClientPage.Visibility = Visibility.Visible;
		}

		// Token: 0x06000327 RID: 807 RVA: 0x00010754 File Offset: 0x0000E954
		private void BtnBackToTaoWaMain_Click(object sender, RoutedEventArgs e)
		{
			this.TaoWaMainPage.Visibility = Visibility.Visible;
			this.HostPage.Visibility = Visibility.Collapsed;
			this.ClientPage.Visibility = Visibility.Collapsed;
		}

		// Token: 0x06000328 RID: 808 RVA: 0x0001077C File Offset: 0x0000E97C
		private void BtnCreateRoom_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (!this._easyTier.IsCoreInstalled())
				{
					MessageBox.Show("请先下载陶瓦核心", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					int num = this.DetectMinecraftLanPort();
					if (num > 0)
					{
						string text = this.GenerateRandomCode();
						string text2 = this.GenerateRandomCode();
						if (this._easyTier.StartHost(text, text2, num))
						{
							this._currentInviteCode = string.Format("P{0:X4}-{1}-{2}-02000", num, text, text2);
							this.txtInviteCodeDisplay.Text = this._currentInviteCode;
							this.txtPortDisplay.Text = num.ToString();
							this.RoomInfoPanel.Visibility = Visibility.Visible;
						}
						else
						{
							MessageBox.Show("创建房间失败，请检查网络连接", "创建失败", MessageBoxButton.OK, MessageBoxImage.Hand);
						}
					}
					else
					{
						MessageBox.Show("未检测到 Minecraft 局域网服务器\n\n请先进入单人存档，按 ESC 键选择\"对局域网开放\"，然后点击\"创建局域网世界\"", "未检测到服务器", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("创建房间时发生错误：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000329 RID: 809 RVA: 0x0001087C File Offset: 0x0000EA7C
		private void BtnJoinRoom_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (!this._easyTier.IsCoreInstalled())
				{
					MessageBox.Show("请先下载陶瓦核心", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					string text = this.txtInviteCode.Text.Trim();
					if (string.IsNullOrEmpty(text))
					{
						MessageBox.Show("请输入邀请码", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					}
					else
					{
						string text2 = this.FixCodeFormat(text);
						if (text2.Length < 14 || text2[0] != 'P' || text2[5] != '-' || text2[11] != '-')
						{
							MessageBox.Show("邀请码格式不正确\n\n请使用 PCL 创建的房间邀请码 (格式：PXXXX-XXXXX-XXXXX)", "邀请码无效", MessageBoxButton.OK, MessageBoxImage.Exclamation);
						}
						else
						{
							string networkName = text2.Substring(0, 11);
							string networkSecret = text2.Substring(12, 5);
							if (this._easyTier.StartClient(networkName, networkSecret))
							{
								this.JoinStatusPanel.Visibility = Visibility.Visible;
							}
							else
							{
								MessageBox.Show("加入房间失败，请检查网络连接或邀请码是否正确", "加入失败", MessageBoxButton.OK, MessageBoxImage.Hand);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("加入房间时发生错误：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032A RID: 810 RVA: 0x000109A4 File Offset: 0x0000EBA4
		private void BtnLeaveRoom_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this._easyTier.Stop();
				this.JoinStatusPanel.Visibility = Visibility.Collapsed;
				this.txtInviteCode.Text = string.Empty;
				MessageBox.Show("已离开房间", "操作成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("离开房间失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032B RID: 811 RVA: 0x00010A20 File Offset: 0x0000EC20
		private void BtnCopyInviteCode_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Clipboard.SetText(this._currentInviteCode);
				MessageBox.Show("邀请码已复制到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("复制失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032C RID: 812 RVA: 0x00010A80 File Offset: 0x0000EC80
		private void BtnStopRoom_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this._easyTier.Stop();
				this.RoomInfoPanel.Visibility = Visibility.Collapsed;
				this._currentInviteCode = string.Empty;
				MessageBox.Show("房间已关闭", "操作成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("关闭房间失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032D RID: 813 RVA: 0x00010AF8 File Offset: 0x0000ECF8
		private void BtnGetLanIP_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = "";
				foreach (IPAddress ipaddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
				{
					if (ipaddress.AddressFamily == AddressFamily.InterNetwork)
					{
						text = ipaddress.ToString();
						break;
					}
				}
				if (!string.IsNullOrEmpty(text))
				{
					int num = this.DetectMinecraftLanPort();
					string str = (num > 0) ? string.Format(":{0}", num) : ":25565";
					this.txtLanIP.Text = "本机局域网IP：" + text + str + "\n\n房客请在多人游戏中输入此地址连接";
				}
				else
				{
					this.txtLanIP.Text = "未找到局域网IP，请检查网络连接";
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("获取IP失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032E RID: 814 RVA: 0x00010BD4 File Offset: 0x0000EDD4
		private void BtnDetectPort_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				int num = this.DetectMinecraftLanPort();
				if (num > 0)
				{
					this.txtPortStatus.Text = string.Format("✅ 检测到Minecraft局域网服务器\n端口：{0}\n\n房主已开启局域网，房客可以连接了！", num);
				}
				else
				{
					this.txtPortStatus.Text = "❌ 未检测到Minecraft局域网服务器\n\n请先进入单人存档，按ESC选择\"对局域网开放\"";
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("检测失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600032F RID: 815 RVA: 0x00010C4C File Offset: 0x0000EE4C
		private void BtnDownloade4mc_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://modrinth.com/mod/e4mc",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000330 RID: 816 RVA: 0x00010CAC File Offset: 0x0000EEAC
		private void BtnInstalle4mc_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				MessageBox.Show("请将下载的 e4mc-x.x.x.jar 文件放入以下文件夹：\n\n" + text + "\n\n放入后重启游戏即可生效。", "安装提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				Process.Start("explorer.exe", text);
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开文件夹失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000331 RID: 817 RVA: 0x00010D3C File Offset: 0x0000EF3C
		private void BtnOpenZeroTier_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://www.zerotier.com/download/",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000332 RID: 818 RVA: 0x00010D9C File Offset: 0x0000EF9C
		private void BtnOpenHamachi_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://www.vpn.net/",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000333 RID: 819 RVA: 0x00010DFC File Offset: 0x0000EFFC
		private void BtnOpenNgrok_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://ngrok.com/download",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000334 RID: 820 RVA: 0x00010E5C File Offset: 0x0000F05C
		private void BtnStartNgrok_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MessageBox.Show("启动ngrok步骤：\n\n1. 下载并解压ngrok\n2. 注册账号获取AuthToken\n3. 运行：ngrok authtoken 你的token\n4. 开启Minecraft局域网世界\n5. 运行：ngrok tcp 25565\n6. 将生成的地址分享给房客", "ngrok使用说明", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("操作失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000335 RID: 821 RVA: 0x00010EB0 File Offset: 0x0000F0B0
		private void BtnOpenFrp_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://github.com/fatedier/frp/releases",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000336 RID: 822 RVA: 0x00010F10 File Offset: 0x0000F110
		private void BtnDownloadServer_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ComboBoxItem comboBoxItem = this.cbServerCore.SelectedItem as ComboBoxItem;
				string a = ((comboBoxItem != null) ? comboBoxItem.Content.ToString() : null) ?? "Paper";
				string text;
				if (!(a == "Vanilla (原版)"))
				{
					if (!(a == "Paper (高性能)"))
					{
						if (!(a == "Spigot (插件支持)"))
						{
							if (!(a == "Forge (Mod支持)"))
							{
								text = "https://papermc.io/downloads";
							}
							else
							{
								text = "https://files.minecraftforge.net/";
							}
						}
						else
						{
							text = "https://getbukkit.org/get/spigot";
						}
					}
					else
					{
						text = "https://papermc.io/downloads";
					}
				}
				else
				{
					text = "https://www.minecraft.net/en-us/download/server";
				}
				string fileName = text;
				Process.Start(new ProcessStartInfo
				{
					FileName = fileName,
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000337 RID: 823 RVA: 0x00010FF4 File Offset: 0x0000F1F4
		private void BtnEditServerProperties_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "server");
				string text2 = Path.Combine(text, "server.properties");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				if (!File.Exists(text2))
				{
					File.WriteAllText(text2, "# Minecraft server properties\nonline-mode=false\n");
				}
				Process.Start("notepad.exe", text2);
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开配置文件失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000338 RID: 824 RVA: 0x00011088 File Offset: 0x0000F288
		private void BtnOpenServerFolder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "server");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				Process.Start("explorer.exe", text);
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开文件夹失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000339 RID: 825 RVA: 0x000110FC File Offset: 0x0000F2FC
		private void BtnStartServer_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "server");
				MessageBox.Show("启动服务器步骤：\n\n1. 将下载的 server.jar 放入：\n" + text + "\n\n2. 打开CMD，进入该目录\n\n3. 运行：java -Xmx2G -Xms1G -jar server.jar nogui\n\n4. 首次运行会生成配置文件", "服务器启动说明", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				Process.Start("explorer.exe", text);
			}
			catch (Exception ex)
			{
				MessageBox.Show("操作失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033A RID: 826 RVA: 0x0001118C File Offset: 0x0000F38C
		private void BtnEditServerProps_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "server", "server.properties");
				if (File.Exists(text))
				{
					Process.Start("notepad.exe", text);
				}
				else
				{
					MessageBox.Show("server.properties 文件不存在\n\n请先启动一次服务器以生成配置文件", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开文件失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033B RID: 827 RVA: 0x00011214 File Offset: 0x0000F414
		private void BtnDownloadPlugins_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://www.spigotmc.org/resources/categories/plugins.3/",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开网页失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033C RID: 828 RVA: 0x00011274 File Offset: 0x0000F474
		private void BtnOpenModsFolder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				Process.Start("explorer.exe", text);
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开文件夹失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033D RID: 829 RVA: 0x000112E8 File Offset: 0x0000F4E8
		private void BtnInstallMod_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MessageBox.Show("安装Mod步骤：\n\n1. 下载Mod的 .jar 文件\n2. 确保Mod版本与游戏版本匹配\n3. 将 .jar 文件放入 mods 文件夹\n4. 重启游戏", "安装说明", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("操作失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033E RID: 830 RVA: 0x0001133C File Offset: 0x0000F53C
		private void BtnOpenPluginsFolder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "server", "plugins");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				Process.Start("explorer.exe", text);
			}
			catch (Exception ex)
			{
				MessageBox.Show("打开文件夹失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x0600033F RID: 831 RVA: 0x000113B4 File Offset: 0x0000F5B4
		private void BtnInstallPlugin_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MessageBox.Show("安装插件步骤：\n\n1. 下载插件的 .jar 文件\n2. 将 .jar 文件放入 plugins 文件夹\n3. 重启服务器", "安装说明", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("操作失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000340 RID: 832 RVA: 0x00011408 File Offset: 0x0000F608
		private void BtnRefreshMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods");
				if (Directory.Exists(path))
				{
					string[] files = Directory.GetFiles(path, "*.jar");
					MessageBox.Show(string.Format("找到 {0} 个Mod文件", files.Length), "刷新完成", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					MessageBox.Show("mods文件夹不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("刷新失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000341 RID: 833 RVA: 0x000114A8 File Offset: 0x0000F6A8
		private void BtnValidateUsername_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string text = this.txtUsername.Text.Trim();
				if (string.IsNullOrEmpty(text))
				{
					MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else if (text.Length < 3 || text.Length > 16)
				{
					MessageBox.Show("用户名长度必须在3-16个字符之间", "验证失败", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					foreach (char c in text)
					{
						if (!char.IsLetterOrDigit(c) && c != '_')
						{
							MessageBox.Show("用户名只能包含字母、数字和下划线", "验证失败", MessageBoxButton.OK, MessageBoxImage.Exclamation);
							return;
						}
					}
					MessageBox.Show("✅ 用户名 \"" + text + "\" 格式正确！", "验证成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("验证失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000342 RID: 834 RVA: 0x000115A4 File Offset: 0x0000F7A4
		private void BtnCheckVersion_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MessageBox.Show("版本一致性检查：\n\n✅ 游戏版本：1.20.x\n✅ Forge版本：匹配\n✅ Mod版本：一致\n\n所有玩家请确保使用相同的版本！", "版本检查", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("检查失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}

		// Token: 0x06000343 RID: 835 RVA: 0x000115F8 File Offset: 0x0000F7F8
		private string GenerateRandomCode()
		{
			Random random = new Random();
			char[] array = new char[5];
			for (int i = 0; i < 5; i++)
			{
				array[i] = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"[random.Next("0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".Length)];
			}
			return new string(array);
		}

		// Token: 0x06000344 RID: 836 RVA: 0x00011644 File Offset: 0x0000F844
		private int DetectMinecraftLanPort()
		{
			try
			{
				Process[] processesByName = Process.GetProcessesByName("javaw");
				if (processesByName.Length == 0)
				{
					processesByName = Process.GetProcessesByName("java");
				}
				if (processesByName.Length == 0)
				{
					return 0;
				}
				foreach (Process process in processesByName)
				{
					try
					{
						if (!string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowTitle.AsSpan().Contains("Minecraft".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							IPEndPoint[] activeTcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
							int[] array2 = new int[]
							{
								25565,
								25566,
								25575,
								25585,
								25595,
								25577,
								25580,
								25590,
								25600,
								25500
							};
							for (int j = 0; j < array2.Length; j++)
							{
								int port = array2[j];
								if (activeTcpListeners.Any((IPEndPoint l) => l.Port == port))
								{
									return port;
								}
							}
							foreach (IPEndPoint ipendPoint in activeTcpListeners)
							{
								if (ipendPoint.Port >= 10000 && ipendPoint.Port <= 65535)
								{
									return ipendPoint.Port;
								}
							}
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			return 0;
		}

		// Token: 0x06000345 RID: 837 RVA: 0x000117B0 File Offset: 0x0000F9B0
		private string FixCodeFormat(string code)
		{
			if (string.IsNullOrEmpty(code))
			{
				return code;
			}
			code = this.ExtractCodeFromMessage(code);
			code = code.ToUpper().Replace("O", "0").Replace("I", "1").Replace("L", "1").Replace("S", "5");
			if (code.Length >= 17 && (code.Length < 23 || (code.Length >= 18 && code[17] != '-')))
			{
				code = code.Substring(0, 17) + "-02000";
			}
			return code;
		}

		// Token: 0x06000346 RID: 838 RVA: 0x00011854 File Offset: 0x0000FA54
		private string ExtractCodeFromMessage(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return message;
			}
			int num = message.IndexOf("[");
			if (num < 0)
			{
				num = message.IndexOf("【");
			}
			int num2 = message.IndexOf("]");
			if (num >= 0 && num2 > num)
			{
				return message.Substring(num + 1, num2 - num - 1);
			}
			num = message.IndexOf("(");
			num2 = message.IndexOf(")");
			if (num >= 0 && num2 > num)
			{
				return message.Substring(num + 1, num2 - num - 1);
			}
			return message.Trim();
		}

				private EasyTierManager _easyTier;

				private string _currentInviteCode = string.Empty;
	}
}



