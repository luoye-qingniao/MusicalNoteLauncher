using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Core;
using Newtonsoft.Json.Linq;
using PCL.Account;

namespace MusicalNoteLauncher.Windows
{
    /// <summary>
    /// 青鸟账号登录弹窗 —— 使用邮箱 + 密码登录
    /// </summary>
    public class QingNiaoLoginResult
    {
        public string UserName { get; set; }
        public string Uuid { get; set; }
        public string AccessToken { get; set; }
        public string ClientToken { get; set; }
        public string ServerUrl { get; set; }
    }

    public class QingNiaoLoginDialog : Window
    {
        private TextBox _txtEmail;
        private PasswordBox _txtPassword;
        private TextBlock _lblStatus;
        private TextBlock _lblServerHint;
        private Button _btnLogin;
        private bool _isLoggingIn;
        private string _serverUrl;

        public event Action<QingNiaoLoginResult> OnLoginSuccess;

        public QingNiaoLoginDialog()
        {
            Title = "邮箱登录";
            Width = 400;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            _serverUrl = AccountManager.GetQingNiaoServer();
            if (string.IsNullOrEmpty(_serverUrl))
                _serverUrl = PCL.Account.Settings.Get<string>("CacheQingNiaoServer");
            if (string.IsNullOrEmpty(_serverUrl))
                _serverUrl = "85.137.246.87";

            string cachedUser = PCL.Account.Settings.Get<string>("CacheQingNiaoEmail");
            string cachedPass = SecureStorage.Unprotect(PCL.Account.Settings.Get<string>("CacheQingNiaoPass"));

            BuildContent(cachedUser, cachedPass);

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) AnimateAndClose(); };
        }

        private void BuildContent(string cachedUser, string cachedPass)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(28),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.9, 0.9)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0: 标题
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });   // 1: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 2: 服务器提示
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });  // 3: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 4: 邮箱
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });  // 5: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 6: 密码
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });  // 7: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 8: 状态
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });  // 9: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 10: 按钮

            // 标题栏
            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.Children.Add(new TextBlock
            {
                Text = "邮箱登录",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var closeBtn = new Button
            {
                Content = "✕", Width = 30, Height = 30, FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FontFamily = new FontFamily("Microsoft YaHei")
            };
            closeBtn.Click += (s, e) => AnimateAndClose();
            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = new SolidColorBrush(Colors.White);
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(closeBtn);
            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            // 服务器提示
            _lblServerHint = new TextBlock
            {
                Text = "认证服务器: 青鸟官方服务器",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            Grid.SetRow(_lblServerHint, 2);
            grid.Children.Add(_lblServerHint);

            // 邮箱
            Grid.SetRow(BuildInputRow("邮箱", "输入你的邮箱", out _txtEmail, cachedUser), 4);
            grid.Children.Add(_txtEmail.Parent as UIElement);

            // 密码
            var pwdRow = BuildPasswordRow("密码", out _txtPassword, cachedPass);
            Grid.SetRow(pwdRow, 6);
            grid.Children.Add(pwdRow);

            // 状态提示
            _lblStatus = new TextBlock
            {
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(_lblStatus, 8);
            grid.Children.Add(_lblStatus);

            // 登录按钮
            _btnLogin = new Button
            {
                Content = "登  录",
                Height = 40,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            _btnLogin.Click += BtnLogin_Click;
            Grid.SetRow(_btnLogin, 10);
            grid.Children.Add(_btnLogin);

            border.Child = grid;
            Content = border;

            Loaded += (s, e) =>
            {
                var scaleAnim = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var opacityAnim = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(180));
                border.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                border.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                border.BeginAnimation(OpacityProperty, opacityAnim);
            };
        }

        private Grid BuildInputRow(string label, string placeholder, out TextBox textBox, string prefill = null)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontFamily = new FontFamily("Microsoft YaHei")
            });

            var tb = new TextBox
            {
                Height = 36, FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Background = new SolidColorBrush(Color.FromRgb(56, 56, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 86)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == placeholder) { tb.Text = ""; tb.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)); }
            };
            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text)) { tb.Text = placeholder; tb.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)); }
            };

            if (!string.IsNullOrEmpty(prefill))
            {
                tb.Text = prefill;
                tb.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            }
            else
            {
                tb.Text = placeholder;
                tb.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            }

            textBox = tb;
            Grid.SetRow(tb, 2);
            grid.Children.Add(tb);

            return grid;
        }

        private Grid BuildPasswordRow(string label, out PasswordBox passwordBox, string prefill = null)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontFamily = new FontFamily("Microsoft YaHei")
            });

            passwordBox = new PasswordBox
            {
                Height = 36, FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Background = new SolidColorBrush(Color.FromRgb(56, 56, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 86)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                PasswordChar = '●'
            };

            if (!string.IsNullOrEmpty(prefill))
                passwordBox.Password = prefill;

            Grid.SetRow(passwordBox, 2);
            grid.Children.Add(passwordBox);

            return grid;
        }


        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn) return;

            string email = _txtEmail != null ?
                (_txtEmail.Text == "输入你的邮箱" ? "" : _txtEmail.Text.Trim()) : "";
            string password = _txtPassword?.Password ?? "";

            if (string.IsNullOrEmpty(email)) { SetStatus("请输入邮箱", false); return; }
            if (string.IsNullOrEmpty(password)) { SetStatus("请输入密码", false); return; }

            string serverUrl = _serverUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            _isLoggingIn = true;
            _btnLogin.IsEnabled = false;
            _btnLogin.Content = "登录中...";
            SetStatus("正在验证账号...", true);

            try
            {
                var result = await AuthenticateAsync(serverUrl, email, password,
                    PCL.Account.Settings.Get<string>("CacheQingNiaoClient") ?? Guid.NewGuid().ToString("N"));

                SetStatus("登录成功！", true);

                PCL.Account.AccountManager.SaveServerLogin("QingNiao",
                    new PCL.Account.McLoginResult
                    {
                        Name = result.UserName, Uuid = result.Uuid,
                        AccessToken = result.AccessToken, ClientToken = result.ClientToken, Type = "QingNiao"
                    }, email, password);

                PCL.Account.AccountManager.SaveServerLoginRecord("QingNiao", email, password);
                PCL.Account.Settings.Set("CacheQingNiaoServer", serverUrl);
                PCL.Account.Settings.Set("CacheQingNiaoEmail", email);
                PCL.Account.Settings.Set("CacheQingNiaoPass", SecureStorage.Protect(password));
                PCL.Account.Settings.Set("CacheQingNiaoClient", result.ClientToken);

                // 持久化服务器返回的真实青鸟ID和名称
                PCL.Account.Settings.Set("QingniaoId", result.Uuid);
                PCL.Account.Settings.Set("QingniaoName", result.UserName);

                // 同步到 MNL 主配置 (SettingsManager)，确保 GetProfileJson 验证通过
                Core.SettingsManager.Settings.QingniaoId = result.Uuid;
                Core.SettingsManager.Settings.QingniaoName = result.UserName;
                Core.SettingsManager.Settings.QingniaoEmail = email;
                Core.SettingsManager.Settings.QingniaoPass = password;
                Core.SettingsManager.Settings.QingniaoServer = serverUrl;
                Core.SettingsManager.Settings.QingniaoClientToken = result.ClientToken;
                Core.SettingsManager.SaveSettings();

                // 立即更新 AppContext（WebView 的 GetProfileJson 优先读 AppContext）
                MusicalNoteLauncher.AppContext.QingniaoId = result.Uuid;
                MusicalNoteLauncher.AppContext.QingniaoName = result.UserName;
                MusicalNoteLauncher.AppContext.SetQingniaoName(result.UserName);

                OnLoginSuccess?.Invoke(result);
                await Task.Delay(500);
                AnimateAndClose();
            }
            catch (Exception ex)
            {
                SetStatus($"登录失败: {ex.Message}", false);
                _isLoggingIn = false;
                _btnLogin.IsEnabled = true;
                _btnLogin.Content = "登  录";
            }
        }

        private async Task<QingNiaoLoginResult> AuthenticateAsync(
            string serverUrl, string email, string password, string clientToken)
        {
            string authUrl = $"http://{serverUrl}/api/auth.php?action=login";

            var payload = new JObject
            {
                ["email"] = email,
                ["password"] = password,
                ["client_id"] = clientToken
            };

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try { response = await client.PostAsync(authUrl, content); }
                catch (HttpRequestException) { throw new Exception("无法连接到认证服务器"); }
                catch (TaskCanceledException) { throw new Exception("连接超时"); }

                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    try { throw new Exception(JObject.Parse(body)["error"]?.ToString() ?? $"错误 ({(int)response.StatusCode})"); }
                    catch (Exception ex) when (ex is not System.Text.Json.JsonException) { throw; }
                    catch { throw new Exception($"服务器返回错误 ({(int)response.StatusCode})"); }
                }

                var json = JObject.Parse(body);
                string accessToken = json["token"]?.ToString();
                var user = json["user"];

                if (string.IsNullOrEmpty(accessToken) || user == null)
                    throw new Exception("服务器返回数据不完整");

                return new QingNiaoLoginResult
                {
                    UserName = user["nickname"]?.ToString() ?? email,
                    Uuid = user["qingniao_id"]?.ToString() ?? email,
                    AccessToken = accessToken,
                    ClientToken = clientToken,
                    ServerUrl = serverUrl
                };
            }
        }

        public void SetStatus(string message, bool isSuccess)
        {
            Dispatcher.Invoke(() =>
            {
                _lblStatus.Text = message;
                _lblStatus.Foreground = new SolidColorBrush(
                    isSuccess ? Color.FromRgb(76, 175, 80) : Color.FromRgb(239, 83, 80));
            });
        }

        private void AnimateAndClose()
        {
            if (Content is Border border)
            {
                var scaleAnim = new DoubleAnimation(1.0, 0.9, TimeSpan.FromMilliseconds(120))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100));
                opacityAnim.Completed += (s, e) => Close();
                border.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                border.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                border.BeginAnimation(OpacityProperty, opacityAnim);
            }
            else { Close(); }
        }
    }
}
