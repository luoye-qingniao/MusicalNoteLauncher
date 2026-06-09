using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
		public partial class LoginPage : UserControl
	{
								public event Action<string, bool> OnLoginSuccess;

		// Token: 0x060002B0 RID: 688 RVA: 0x0000DB25 File Offset: 0x0000BD25
		public LoginPage() : this(AppContext.Config) { }

		public LoginPage(ConfigManager config)
		{
			this.InitializeComponent();
			if (config == null)
			{
				throw new ArgumentNullException("config");
			}
			this._config = config;
			base.Loaded += this.LoginPage_Loaded;
		}

		// Token: 0x060002B1 RID: 689 RVA: 0x0000DB5B File Offset: 0x0000BD5B
		private void LoginPage_Loaded(object sender, RoutedEventArgs e)
		{
			this.LoadSavedAccount();
		}

		// Token: 0x060002B2 RID: 690 RVA: 0x0000DB64 File Offset: 0x0000BD64
		private void LoadSavedAccount()
		{
			try
			{
				if (this._config == null)
				{
					Logger.Error("ConfigManager is null in LoadSavedAccount");
				}
				else if (this._config.RememberAccount && !string.IsNullOrEmpty(this._config.Username))
				{
					if (this.txtUsername != null)
					{
						this.txtUsername.Text = (this._config.Username ?? string.Empty);
					}
					if (this.chkRemember != null)
					{
						this.chkRemember.IsChecked = new bool?(this._config.RememberAccount);
					}
					if (this.togOfflineMode != null)
					{
						this.togOfflineMode.IsChecked = new bool?(this._config.OfflineMode);
					}
					Logger.Info("Loaded saved account: " + this._config.Username);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Error loading saved account: " + ex.Message);
			}
		}

		// Token: 0x060002B3 RID: 691 RVA: 0x0000DC60 File Offset: 0x0000BE60
		private void BtnLogin_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (this.txtUsername == null)
				{
					this.ShowModernMessage("用户名输入框初始化失败", "错误");
				}
				else
				{
					string text = this.txtUsername.Text;
					string text2 = ((text != null) ? text.Trim() : null) ?? string.Empty;
					if (string.IsNullOrEmpty(text2))
					{
						this.ShowModernMessage("请输入用户名", "提示");
						this.txtUsername.Focus();
					}
					else if (text2.Length < 3 || text2.Length > 16)
					{
						this.ShowModernMessage("用户名长度需要在3-16个字符之间", "提示");
						this.txtUsername.Focus();
					}
					else if (!this.IsValidUsername(text2))
					{
						this.ShowModernMessage("用户名只能包含字母、数字和下划线", "提示");
						this.txtUsername.Focus();
					}
					else if (this._config == null)
					{
						this.ShowModernMessage("配置管理器初始化失败", "错误");
					}
					else
					{
						this._config.Username = text2;
						ConfigManager config = this._config;
						CheckBox checkBox = this.chkRemember;
						config.RememberAccount = (checkBox != null && checkBox.IsChecked.GetValueOrDefault());
						ConfigManager config2 = this._config;
						ToggleButton toggleButton = this.togOfflineMode;
						config2.OfflineMode = (toggleButton != null && toggleButton.IsChecked.GetValueOrDefault());
						this._config.Save();
						Logger.Info(string.Format("User logged in: {0}, OfflineMode: {1}", text2, this._config.OfflineMode));
						ToggleButton toggleButton2 = this.togOfflineMode;
						bool arg = toggleButton2 != null && toggleButton2.IsChecked.GetValueOrDefault();
						Action<string, bool> onLoginSuccess = this.OnLoginSuccess;
						if (onLoginSuccess != null)
						{
							onLoginSuccess(text2, arg);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Login error: " + ex.Message);
				this.ShowModernMessage("登录时发生错误：" + ex.Message, "错误");
			}
		}

		// Token: 0x060002B4 RID: 692 RVA: 0x0000DE50 File Offset: 0x0000C050
		private bool IsValidUsername(string username)
		{
			if (string.IsNullOrEmpty(username))
			{
				return false;
			}
			foreach (char c in username)
			{
				if (!char.IsLetterOrDigit(c) && c != '_')
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x060002B5 RID: 693 RVA: 0x0000DE94 File Offset: 0x0000C094
		private void ShowModernMessage(string message, string title)
		{
			try
			{
				MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				Logger.Error("Error showing message box: " + ex.Message);
			}
		}

				private readonly ConfigManager _config;
	}
}



