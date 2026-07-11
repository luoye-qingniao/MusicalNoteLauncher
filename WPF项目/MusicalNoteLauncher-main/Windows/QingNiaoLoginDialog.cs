using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;
using PCL.Account;

namespace MusicalNoteLauncher.Windows
{
    /// <summary>
    /// 青鸟账号登录弹窗 —— 基于 Authlib-Injector 协议的第三方认证，支持邮箱验证码辅助登录
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
        private TextBox _txtUsername;
        private PasswordBox _txtPassword;
        private TextBox _txtVerifyCode;
        private TextBlock _lblStatus;
        private TextBlock _lblServerHint;
        private Button _btnSendCode;
        private Button _btnLogin;
        private bool _isLoggingIn;
        private bool _isSendingCode;
        private string _serverUrl;

        public event Action<QingNiaoLoginResult> OnLoginSuccess;

        public QingNiaoLoginDialog()
        {
            Title = "青鸟账号登录";
            Width = 400;
            Height = 440;
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

            string cachedUser = PCL.Account.Settings.Get<string>("CacheQingNiaoUsername");
            string cachedPass = PCL.Account.Settings.Get<string>("CacheQingNiaoPass");

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
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 4: 账号
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });  // 5: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 6: 密码
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });  // 7: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 8: 验证码标签
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });   // 9: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 10: 验证码输入
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });  // 11: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 12: 状态
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });  // 13: 间距
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 14: 按钮

            // 标题栏
            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.Children.Add(new TextBlock
            {
                Text = "🐦 青鸟账号登录",
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

            // 账号
            Grid.SetRow(BuildInputRow("邮箱 / 用户名", "输入你的青鸟账号", out _txtUsername, cachedUser), 4);
            grid.Children.Add(_txtUsername.Parent as UIElement);

            // 密码
            var pwdRow = BuildPasswordRow("密码", out _txtPassword, cachedPass);
            Grid.SetRow(pwdRow, 6);
            grid.Children.Add(pwdRow);

            // 验证码标签
            var verifyLabel = new TextBlock
            {
                Text = "邮件验证码",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(verifyLabel, 8);
            grid.Children.Add(verifyLabel);

            // 验证码输入行
            var verifyInputRow = new DockPanel();
            _txtVerifyCode = new TextBox
            {
                Height = 36, FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Background = new SolidColorBrush(Color.FromRgb(56, 56, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 86)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            string verifyPlaceholder = "输入邮件中的验证码";
            _txtVerifyCode.GotFocus += (s, e) =>
            {
                if (_txtVerifyCode.Text == verifyPlaceholder) { _txtVerifyCode.Text = ""; _txtVerifyCode.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)); }
            };
            _txtVerifyCode.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtVerifyCode.Text)) { _txtVerifyCode.Text = verifyPlaceholder; _txtVerifyCode.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)); }
            };
            _txtVerifyCode.Text = verifyPlaceholder;
            _txtVerifyCode.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            DockPanel.SetDock(_txtVerifyCode, Dock.Left);
            verifyInputRow.Children.Add(_txtVerifyCode);

            _btnSendCode = new Button
            {
                Content = "发送",
                Width = 56, Height = 36,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            _btnSendCode.Click += BtnSendCode_Click;
            DockPanel.SetDock(_btnSendCode, Dock.Right);
            verifyInputRow.Children.Add(_btnSendCode);

            Grid.SetRow(verifyInputRow, 10);
            grid.Children.Add(verifyInputRow);

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
            Grid.SetRow(_lblStatus, 12);
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
            Grid.SetRow(_btnLogin, 14);
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


        /// <summary>
        /// 发送邮件验证码
        /// </summary>
        private async void BtnSendCode_Click(object sender, RoutedEventArgs e)
        {
            if (_isSendingCode) return;

            string username = _txtUsername != null ?
                (_txtUsername.Text == "输入你的青鸟账号" ? "" : _txtUsername.Text.Trim()) : "";
            string password = _txtPassword?.Password ?? "";

            if (string.IsNullOrEmpty(username)) { SetStatus("请输入邮箱/用户名", false); return; }
            if (string.IsNullOrEmpty(password)) { SetStatus("请输入密码", false); return; }

            string serverUrl = _serverUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            _isSendingCode = true;
            _btnSendCode.IsEnabled = false;
            _btnSendCode.Content = "...";
            SetStatus("正在发送验证码...", true);

            try
            {
                await SendVerificationCodeAsync(serverUrl, username, password);
                SetStatus("验证码已发送，请查收邮箱", true);
            }
            catch (Exception ex)
            {
                SetStatus($"发送失败: {ex.Message}", false);
            }
            finally
            {
                _isSendingCode = false;
                _btnSendCode.IsEnabled = true;
                _btnSendCode.Content = "发送";
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn) return;

            string username = _txtUsername != null ?
                (_txtUsername.Text == "输入你的青鸟账号" ? "" : _txtUsername.Text.Trim()) : "";
            string password = _txtPassword?.Password ?? "";
            string verifyCode = (_txtVerifyCode.Text == "输入邮件中的验证码" ? "" : _txtVerifyCode.Text.Trim());

            if (string.IsNullOrEmpty(username)) { SetStatus("请输入账号", false); return; }
            if (string.IsNullOrEmpty(password)) { SetStatus("请输入密码", false); return; }
            if (string.IsNullOrEmpty(verifyCode)) { SetStatus("请输入邮件验证码", false); return; }

            string serverUrl = _serverUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            _isLoggingIn = true;
            _btnLogin.IsEnabled = false;
            _btnLogin.Content = "登录中...";
            SetStatus("正在验证账号...", true);

            try
            {
                var result = await AuthenticateAsync(serverUrl, username, password, verifyCode,
                    PCL.Account.Settings.Get<string>("CacheQingNiaoClient") ?? Guid.NewGuid().ToString("N"));

                SetStatus("登录成功！", true);

                PCL.Account.AccountManager.SaveServerLogin("QingNiao",
                    new PCL.Account.McLoginResult
                    {
                        Name = result.UserName, Uuid = result.Uuid,
                        AccessToken = result.AccessToken, ClientToken = result.ClientToken, Type = "QingNiao"
                    }, username, password);

                PCL.Account.AccountManager.SaveServerLoginRecord("QingNiao", username, password);
                PCL.Account.Settings.Set("CacheQingNiaoServer", serverUrl);
                PCL.Account.Settings.Set("CacheQingNiaoUsername", username);
                PCL.Account.Settings.Set("CacheQingNiaoPass", password);
                PCL.Account.Settings.Set("CacheQingNiaoClient", result.ClientToken);

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

        private async Task SendVerificationCodeAsync(string serverUrl, string username, string password)
        {
            string url = $"https://{serverUrl}/authserver/send-verification";

            var payload = new JObject { ["username"] = username, ["password"] = password };

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(url, content);
                }
                catch (HttpRequestException) { throw new Exception("无法连接到认证服务器"); }
                catch (TaskCanceledException) { throw new Exception("连接超时"); }

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    try { throw new Exception(JObject.Parse(body)["errorMessage"]?.ToString() ?? $"错误 ({(int)response.StatusCode})"); }
                    catch (Exception ex) when (ex is not System.Text.Json.JsonException) { throw; }
                    catch { throw new Exception($"服务器返回错误 ({(int)response.StatusCode})"); }
                }
            }
        }

        private async Task<QingNiaoLoginResult> AuthenticateAsync(
            string serverUrl, string username, string password, string verifyCode, string clientToken)
        {
            string authUrl = $"https://{serverUrl}/authserver/authenticate";

            var payload = new JObject
            {
                ["agent"] = new JObject { ["name"] = "Minecraft", ["version"] = 1 },
                ["username"] = username,
                ["password"] = password,
                ["verificationCode"] = verifyCode,
                ["clientToken"] = clientToken,
                ["requestUser"] = true
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
                    try { throw new Exception(JObject.Parse(body)["errorMessage"]?.ToString() ?? $"错误 ({(int)response.StatusCode})"); }
                    catch (Exception ex) when (ex is not System.Text.Json.JsonException) { throw; }
                    catch { throw new Exception($"服务器返回错误 ({(int)response.StatusCode})"); }
                }

                var json = JObject.Parse(body);
                string accessToken = json["accessToken"]?.ToString();
                string retClientToken = json["clientToken"]?.ToString();
                var profile = json["selectedProfile"];

                if (string.IsNullOrEmpty(accessToken) || profile?["id"] == null)
                    throw new Exception("服务器返回数据不完整");

                return new QingNiaoLoginResult
                {
                    UserName = profile["name"]?.ToString() ?? username,
                    Uuid = profile["id"].ToString(),
                    AccessToken = accessToken,
                    ClientToken = retClientToken ?? clientToken,
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
