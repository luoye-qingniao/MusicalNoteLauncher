using System;
using System.Windows;
using MusicalNoteLauncher.Pages;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Windows
{
    public partial class LoginWindow : Window
    {
        public string Username { get; private set; }
        public bool IsOfflineMode { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnOfflineLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                ModernMessageBox.ShowWarning("请输入用户名！", "提示");
                return;
            }

            Username = username;
            IsOfflineMode = true;

            MainWindow mainWindow = new MainWindow(Username, IsOfflineMode);
            mainWindow.Show();
            this.Close();
        }

        private void BtnMicrosoftLogin_Click(object sender, RoutedEventArgs e)
        {
            btnMicrosoftLogin.IsEnabled = false;

            var dialog = new MusicalNoteLauncher.Pages.AuthCodeDialog();
            dialog.Owner = this;

            Action<string> browserHandler = null;
            browserHandler = (authUrl) =>
            {
                try { System.Diagnostics.Process.Start(authUrl); } catch { }
            };
            PCL.Auth.Microsoft.MinecraftMsAuth.OnBrowserAuthRequired += browserHandler;

            var authTask = System.Threading.Tasks.Task.Run(async () =>
            {
                return await PCL.Auth.Microsoft.MinecraftMsAuth.LoginAsync("");
            });

            dialog.OnSubmitCode += (codeUrl) =>
            {
                bool ok = PCL.Auth.Microsoft.MinecraftMsAuth.SubmitAuthCode(codeUrl);
                if (ok)
                    dialog.SetStatus("正在验证登录...", true);
                else
                    dialog.SetStatus("未能从 URL 提取授权码，请检查是否复制了完整的地址", false);
                return ok;
            };

            dialog.OnCancel += () => { };

            dialog.Show();

            CloseDialogWhenDone(dialog, authTask, browserHandler, (result) =>
            {
                PCL.Account.AccountManager.SaveMsLogin(
                    result.OAuthRefreshToken,
                    new PCL.Account.McLoginResult
                    {
                        Name = result.UserName,
                        Uuid = result.Uuid,
                        AccessToken = result.McAccessToken,
                        Type = "Microsoft",
                        ProfileJson = result.ProfileJson
                    },
                    result.McExpiresAt
                );

                Username = result.UserName;
                IsOfflineMode = false;

                Dispatcher.Invoke(() =>
                {
                    dialog.Close();
                    MainWindow mainWindow = new MainWindow(Username, IsOfflineMode);
                    mainWindow.Show();
                    this.Close();
                });
            }, (error) =>
            {
                dialog.Dispatcher.Invoke(() => dialog.SetStatus(error, false));
            });
        }

        private async void CloseDialogWhenDone(Window dialog,
            System.Threading.Tasks.Task<PCL.Auth.Microsoft.MsLoginResult> authTask,
            Action<string> browserHandler,
            Action<PCL.Auth.Microsoft.MsLoginResult> onSuccess,
            Action<string> onError)
        {
            try
            {
                var result = await authTask;
                Dispatcher.Invoke(() => dialog.Close());
                onSuccess(result);
            }
            catch (PCL.Auth.Microsoft.MsAuthException ex)
            {
                onError(ex.Message);
            }
            catch (System.Exception ex)
            {
                onError("登录失败: " + ex.Message);
            }
            finally
            {
                PCL.Auth.Microsoft.MinecraftMsAuth.OnBrowserAuthRequired -= browserHandler;
                btnMicrosoftLogin.Dispatcher.Invoke(() => btnMicrosoftLogin.IsEnabled = true);
            }
        }
    }
}