using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Core;
using PCL.Account;
using PCL.Auth.Microsoft;

namespace MusicalNoteLauncher.Pages
{
    public partial class ProfilePage : UserControl
    {
        private ObservableCollection<GameAccount> _gameAccounts = new ObservableCollection<GameAccount>();
        private GameAccount _selectedAccount;
        private int _currentMode = 0; // 0=选择页, 1=青鸟账号, 2=游戏账号

        /// <summary>
        /// 获取当前页面模式
        /// </summary>
        public int CurrentMode => _currentMode;

        /// <summary>
        /// 当用户切换账号时触发，供外部（如 MainWindow）注册回调
        /// </summary>
        public static event Action OnSwitchToAccount;

        public ProfilePage()
        {
            InitializeComponent();
            Loaded += ProfilePage_Loaded;
        }

        public ProfilePage(string username, bool isOfflineMode) : this()
        {
            txtUsername.Text = username;
            txtLoginMode.Text = isOfflineMode ? "离线模式" : "正版模式";
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            AccountListControl.ItemsSource = _gameAccounts;
            LoadGameAccounts();
        }

        #region 页面导航

        private void BtnBackToSelect_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = 0;
            PanelSelect.Visibility = Visibility.Visible;
            PanelQingniao.Visibility = Visibility.Collapsed;
            PanelGameAccount.Visibility = Visibility.Collapsed;
        }

        private void BtnEnterQingniao_Click(object sender, RoutedEventArgs e)
        {
            NavigateToQingNiao();
        }

        private void BtnEnterGameAccount_Click(object sender, RoutedEventArgs e)
        {
            NavigateToGameAccount();
        }

        private void NavigateToQingNiao()
        {
            _currentMode = 1;
            PanelSelect.Visibility = Visibility.Collapsed;
            PanelQingniao.Visibility = Visibility.Visible;
            PanelGameAccount.Visibility = Visibility.Collapsed;
        }

        private void NavigateToGameAccount()
        {
            _currentMode = 2;
            PanelSelect.Visibility = Visibility.Collapsed;
            PanelQingniao.Visibility = Visibility.Collapsed;
            PanelGameAccount.Visibility = Visibility.Visible;
            LoadGameAccounts();
        }

        /// <summary>
        /// 头像区域鼠标左键按下 —— 支持拖拽切换面板（预留）
        /// </summary>
        private void GameAccount_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 预留拖拽功能
        }

        #endregion

        #region 加载与刷新

        /// <summary>
        /// 从 AccountManager 加载所有已持久化的账号
        /// </summary>
        private void LoadGameAccounts()
        {
            _gameAccounts.Clear();

            // 加载离线账号
            foreach (string name in AccountManager.GetLegacyAccounts())
            {
                _gameAccounts.Add(new GameAccount
                {
                    Name = name,
                    Type = AccountType.Offline,
                    Uuid = GenerateOfflineUuid(name)
                });
            }

            // 加载微软账号
            foreach (var ms in AccountManager.GetMsAccounts())
            {
                _gameAccounts.Add(new GameAccount
                {
                    Name = ms.UserName,
                    Type = AccountType.Microsoft,
                    Uuid = ms.Uuid ?? GenerateOfflineUuid(ms.UserName)
                });
            }

            // 加载外置登录账号
            foreach (var record in AccountManager.GetServerLoginRecords("Auth"))
            {
                _gameAccounts.Add(new GameAccount
                {
                    Name = record.Item1,
                    Type = AccountType.AuthlibInjector,
                    AuthServer = AccountManager.GetAuthServer(),
                    Uuid = GenerateOfflineUuid(record.Item1)
                });
            }

            // 恢复当前选中状态
            string currentType = AccountManager.GetLoginType().ToString();
            string currentName = txtUsername.Text;
            var selected = _gameAccounts.FirstOrDefault(a =>
                a.Name == currentName &&
                (a.Type == AccountType.Offline && currentType == "Legacy" ||
                 a.Type == AccountType.Microsoft && currentType == "Ms" ||
                 a.Type == AccountType.AuthlibInjector && currentType == "Auth"));
            if (selected != null)
                SelectAccount(selected);
        }

        /// <summary>
        /// 刷新账号列表（重新加载）
        /// </summary>
        private void RefreshAccountList()
        {
            LoadGameAccounts();
        }

        #endregion

        #region 青鸟账号按钮

        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("编辑个人资料功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnSwitchAccount_Click(object sender, RoutedEventArgs e)
        {
            NavigateToGameAccount();
        }

        private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("快速开始功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnBrowseMods_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("浏览模组功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnGameSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("游戏设置功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        /// <summary>
        /// 动态添加青鸟账号面板的快捷操作卡片（预留扩展）
        /// </summary>
        private void AddQingNiaoFeatureCards()
        {
            // 预留：可在运行时动态添加更多快捷操作按钮
        }

        #endregion

        #region 游戏账号管理

        private void BtnAddOfflineAccount_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OfflineAccountDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                string name = dialog.Username.Trim();
                string uuid = dialog.Uuid.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("玩家名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    return;
                }
                if (name.Length < 3 || name.Length > 16)
                {
                    MessageBox.Show("用户名长度需要在3-16个字符之间", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    return;
                }
                if (_gameAccounts.Any(a => a.Type == AccountType.Offline && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("已存在同名离线账号", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    return;
                }

                AccountManager.SaveLegacyLogin(name);

                var account = new GameAccount
                {
                    Name = name,
                    Type = AccountType.Offline,
                    Uuid = uuid
                };
                _gameAccounts.Add(account);
                SelectAccount(account);
            }
        }

        private async void BtnAddMicrosoftAccount_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            btn.IsEnabled = false;

            try
            {
                var result = await StartMicrosoftAuthAsync(btn);
                if (result != null)
                {
                    // 持久化
                    AccountManager.SaveMsLogin(
                        result.OAuthRefreshToken,
                        new McLoginResult
                        {
                            Name = result.UserName,
                            Uuid = result.Uuid,
                            AccessToken = result.McAccessToken,
                            Type = "Microsoft",
                            ProfileJson = result.ProfileJson
                        },
                        result.McExpiresAt
                    );

                    // 添加到 UI
                    var account = new GameAccount
                    {
                        Name = result.UserName,
                        Type = AccountType.Microsoft,
                        Uuid = result.Uuid
                    };

                    var existing = _gameAccounts.FirstOrDefault(a =>
                        a.Type == AccountType.Microsoft &&
                        a.Name.Equals(result.UserName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        _gameAccounts.Remove(existing);

                    _gameAccounts.Insert(0, account);
                    SelectAccount(account);

                    MessageBox.Show($"微软账号 {result.UserName} 登录成功！", "登录成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (MsAuthException ex)
            {
                MessageBox.Show(ex.Message, "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("登录失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        /// <summary>
        /// 完整的异步微软登录流程
        /// </summary>
        private async System.Threading.Tasks.Task<MsLoginResult> StartMicrosoftAuthAsync(Button btn)
        {
            // 注册浏览器打开事件
            Action<string> browserHandler = null;
            browserHandler = (authUrl) =>
            {
                try { System.Diagnostics.Process.Start(authUrl); } catch { }
            };
            MinecraftMsAuth.OnBrowserAuthRequired += browserHandler;

            try
            {
                // 启动后台认证任务
                var authTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    return await MinecraftMsAuth.LoginAsync("");
                });

                // 显示授权码对话框
                var dialog = await ShowAuthCodeDialogAsync(authTask);

                // 等待登录结果
                var result = await authTask;

                // 成功后关闭对话框
                if (dialog != null && dialog.IsVisible)
                {
                    dialog.Dispatcher.Invoke(() => dialog.Close());
                }

                return result;
            }
            catch (MsAuthException)
            {
                // 让调用方处理异常
                throw;
            }
            catch (Exception ex)
            {
                throw new MsAuthException("登录失败: " + ex.Message, ex);
            }
            finally
            {
                MinecraftMsAuth.OnBrowserAuthRequired -= browserHandler;
            }
        }

        /// <summary>
        /// 显示微软授权码粘贴对话框并连接事件
        /// </summary>
        private System.Threading.Tasks.Task<AuthCodeDialog> ShowAuthCodeDialogAsync(
            System.Threading.Tasks.Task<MsLoginResult> authTask)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<AuthCodeDialog>();
            Dispatcher.Invoke(() =>
            {
                var dialog = new AuthCodeDialog();
                dialog.Owner = Window.GetWindow(this);

                dialog.OnSubmitCode += (codeUrl) =>
                {
                    bool submitted = MinecraftMsAuth.SubmitAuthCode(codeUrl);
                    if (submitted)
                        dialog.SetStatus("正在验证登录...", true);
                    else
                        dialog.SetStatus("未能从 URL 提取授权码，请检查是否复制了完整的地址", false);
                    return submitted;
                };

                dialog.OnCancel += () =>
                {
                    // 用户取消
                };

                dialog.Show();
                tcs.SetResult(dialog);
            });
            return tcs.Task;
        }

        private void BtnAddAuthlibAccount_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new AuthlibInputDialog();
            inputDialog.Owner = Window.GetWindow(this);
            if (inputDialog.ShowDialog() == true)
            {
                string name = inputDialog.Username.Trim();
                string server = inputDialog.AuthServer.Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(server))
                {
                    MessageBox.Show("玩家名称和认证服务器地址不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    return;
                }
                if (_gameAccounts.Any(a => a.Type == AccountType.AuthlibInjector &&
                    a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    a.AuthServer == server))
                {
                    MessageBox.Show("已存在相同的外置登录账号", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    return;
                }

                // 使用 AccountManager 持久化
                AccountManager.SaveServerLoginRecord("Auth", name, "");
                PCL.Account.Settings.Set("CacheAuthServerServer", server);

                var account = new GameAccount
                {
                    Name = name,
                    Type = AccountType.AuthlibInjector,
                    AuthServer = server,
                    Uuid = GenerateOfflineUuid(name)
                };
                _gameAccounts.Add(account);
                SelectAccount(account);
            }
        }

        private void AccountCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameAccount account)
                SelectAccount(account);
        }

        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            GameAccount account = null;
            if (sender is Button btn && btn.Tag is GameAccount acc)
                account = acc;
            else if (_selectedAccount != null)
                account = _selectedAccount;

            if (account != null)
            {
                var result = MessageBox.Show(
                    $"确定要删除账号 \"{account.Name}\" 吗？\n此操作不可撤销。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string loginType;
                    switch (account.Type)
                    {
                        case AccountType.Offline:
                            loginType = "Legacy";
                            break;
                        case AccountType.Microsoft:
                            loginType = "Ms";
                            break;
                        case AccountType.AuthlibInjector:
                            loginType = "Auth";
                            break;
                        default:
                            loginType = "Legacy";
                            break;
                    }
                    AccountManager.RemoveAccount(loginType, account.Name);

                    _gameAccounts.Remove(account);
                    SelectAccount(null);
                }
            }
        }

        /// <summary>
        /// 选中指定账号并同步到全局上下文
        /// </summary>
        private void SelectAccount(GameAccount account)
        {
            foreach (var a in _gameAccounts)
                a.IsSelected = false;

            _selectedAccount = account;
            if (account != null)
            {
                account.IsSelected = true;

                // 更新启动器当前账号
                MusicalNoteLauncher.AppContext.Username = account.Name;
                MusicalNoteLauncher.AppContext.IsOfflineMode = account.Type == AccountType.Offline;

                // 设置 AccountManager 的当前登录类型
                McLoginType loginType;
                switch (account.Type)
                {
                    case AccountType.Offline:
                        loginType = McLoginType.Legacy;
                        break;
                    case AccountType.Microsoft:
                        loginType = McLoginType.Ms;
                        break;
                    case AccountType.AuthlibInjector:
                        loginType = McLoginType.Auth;
                        break;
                    default:
                        loginType = McLoginType.Legacy;
                        break;
                }
                AccountManager.SetLoginType(loginType);

                // 同步到青鸟账号页
                txtUsername.Text = account.Name;
                txtLoginMode.Text = account.TypeDisplay + "模式";

                // 触发切换账号回调
                OnSwitchToAccount?.Invoke();
            }
        }

        /// <summary>
        /// 获取账号类型对应的颜色画刷
        /// </summary>
        private Brush GetTypeColor(AccountType type)
        {
            var converter = new AccountTypeColorConverter();
            return (Brush)converter.Convert(type, typeof(Brush), null, System.Globalization.CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// 生成离线模式 UUID（MD5 of "OfflinePlayer:" + username）
        /// </summary>
        private string GenerateOfflineUuid(string username)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        #endregion
    }

    #region 离线账号创建对话框

    /// <summary>
    /// 离线账号创建对话框 —— 含名称提示和高级 UUID 设置
    /// </summary>
    public class OfflineAccountDialog : Window
    {
        public string Username { get; private set; }
        public string Uuid { get; private set; }

        private TextBox _txtName;
        private TextBox _txtUuid;
        private TextBlock _lblNameHint;
        private Border _panelAdvanced;
        private bool _advancedExpanded;

        public OfflineAccountDialog()
        {
            Title = "创建离线账号";
            Width = 480;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;

            var border = new Border
            {
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2D2D2D")),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.9, 0.9)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题
            var lblTitle = new TextBlock
            {
                Text = "创建离线账号",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            Grid.SetRow(lblTitle, 0);
            grid.Children.Add(lblTitle);

            // 名称输入标签
            var lblName = new TextBlock
            {
                Text = "玩家名称",
                FontSize = 13,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#BBBBBB")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(lblName, 2);
            grid.Children.Add(lblName);

            // 名称输入框
            _txtName = new TextBox
            {
                Text = "Steve",
                FontSize = 14,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#1E1E1E")),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Height = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CaretBrush = Brushes.White,
                MaxLength = 16
            };
            _txtName.SelectAll();
            _txtName.TextChanged += (s, ev) => UpdateNameHint();
            Grid.SetRow(_txtName, 4);
            grid.Children.Add(_txtName);

            // 名称提示
            _lblNameHint = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FF9800")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 4, 0, 0)
            };
            Grid.SetRow(_lblNameHint, 6);
            grid.Children.Add(_lblNameHint);

            // 高级设置展开按钮
            var btnAdvanced = new Button
            {
                Content = "▶ 高级设置",
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#888888")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            btnAdvanced.Click += (s, ev) =>
            {
                _advancedExpanded = !_advancedExpanded;
                _panelAdvanced.Visibility = _advancedExpanded ? Visibility.Visible : Visibility.Collapsed;
                btnAdvanced.Content = _advancedExpanded ? "▼ 高级设置" : "▶ 高级设置";
                Height = _advancedExpanded ? 540 : 380;
            };
            Grid.SetRow(btnAdvanced, 8);
            grid.Children.Add(btnAdvanced);

            // 高级设置面板
            _panelAdvanced = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#1E1E1E")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 0)
            };

            var advancedGrid = new Grid();
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            advancedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // UUID 标签
            var lblUuid = new TextBlock
            {
                Text = "UUID",
                FontSize = 13,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#BBBBBB")),
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            advancedGrid.Children.Add(lblUuid);

            // UUID 输入框
            _txtUuid = new TextBox
            {
                FontSize = 12,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2A2A2A")),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CaretBrush = Brushes.White,
                MaxLength = 36
            };
            Grid.SetRow(_txtUuid, 2);
            _txtUuid.TextChanged += (s, ev) => UpdateUuidOnName();
            advancedGrid.Children.Add(_txtUuid);

            // UUID 说明文字（简短版先放这里，下方面板放详细说明）
            var lblUuidDescShort = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#666666")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            Grid.SetRow(lblUuidDescShort, 4);
            advancedGrid.Children.Add(lblUuidDescShort);

            // UUID 提示 + 重新生成按钮
            var uuidBtnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var lblUuidHint = new TextBlock
            {
                Text = "UUID 是 Minecraft 玩家的唯一标识符。每个启动器生成 UUID 的方式可能不同。通过将 UUID 修改为原启动器所生成的 UUID，你可以保证在切换启动器后，游戏还能将你的游戏角色识别为给定 UUID 所对应的角色，从而保留原角色的背包物品。UUID 选项为高级选项。除非你知道你在做什么，否则你不需要调整该选项。",
                FontSize = 11,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#888888")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            var btnRefreshUuid = new Button
            {
                Content = "重新生成",
                FontSize = 11,
                Width = 70,
                Height = 26,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Top
            };
            btnRefreshUuid.Click += (s, ev) =>
            {
                string uname = _txtName.Text.Trim();
                if (!string.IsNullOrWhiteSpace(uname))
                    _txtUuid.Text = GenerateOfflineUuidStatic(uname);
            };
            uuidBtnPanel.Children.Add(lblUuidHint);
            uuidBtnPanel.Children.Add(btnRefreshUuid);
            Grid.SetRow(uuidBtnPanel, 6);
            advancedGrid.Children.Add(uuidBtnPanel);

            _panelAdvanced.Child = advancedGrid;
            Grid.SetRow(_panelAdvanced, 10);
            grid.Children.Add(_panelAdvanced);

            // 按钮面板
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, ev) => { DialogResult = false; Close(); };

            var btnOk = new Button
            {
                Content = "创建账号",
                Width = 100,
                Height = 36,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2ECC71")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, ev) =>
            {
                Username = _txtName.Text.Trim();
                Uuid = string.IsNullOrWhiteSpace(_txtUuid.Text)
                    ? GenerateOfflineUuidStatic(Username)
                    : _txtUuid.Text.Trim().Replace("-", "");
                DialogResult = true;
                Close();
            };

            _txtName.KeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    Username = _txtName.Text.Trim();
                    Uuid = string.IsNullOrWhiteSpace(_txtUuid.Text)
                        ? GenerateOfflineUuidStatic(Username)
                        : _txtUuid.Text.Trim().Replace("-", "");
                    DialogResult = true;
                    Close();
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 11);
            grid.Children.Add(btnPanel);

            border.Child = grid;
            Content = border;

            Loaded += (s, ev) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleX = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleY = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(Window.OpacityProperty, fadeIn);
                var st = (ScaleTransform)((Border)Content).RenderTransform;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                _txtName.Focus();
                _txtName.SelectAll();
                UpdateNameHint();
            };
        }

        private void UpdateNameHint()
        {
            string name = _txtName.Text;
            var msgs = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                _lblNameHint.Text = "";
                _lblNameHint.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FF9800"));
                return;
            }

            bool hasInvalid = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '_';
                if (!valid) { hasInvalid = true; break; }
            }

            if (hasInvalid)
                msgs.Add("含有不支持的字符，仅允许英文字母、数字及下划线");

            if (name.Length < 3)
                msgs.Add("名称过短（至少 3 个字符）");
            if (name.Length > 16)
                msgs.Add("名称过长（最多 16 个字符）");

            if (msgs.Count > 0)
            {
                _lblNameHint.Text = "⚠ " + string.Join("；", msgs) + "，可能导致启动失败或无法加入联机游戏";
                _lblNameHint.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FF5722"));
            }
            else
            {
                _lblNameHint.Text = "✓ 名称格式正确";
                _lblNameHint.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#4CAF50"));
            }

            UpdateUuidOnName();
        }

        private void UpdateUuidOnName()
        {
            if (!_advancedExpanded || _txtUuid == null) return;
            string name = _txtName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(name) && _txtUuid.Text.Length == 0)
                _txtUuid.Text = GenerateOfflineUuidStatic(name);
        }

        private static string GenerateOfflineUuidStatic(string username)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }

    #endregion

    #region 外置登录对话框

    /// <summary>
    /// 外置登录（Authlib-Injector）输入对话框
    /// </summary>
    public class AuthlibInputDialog : Window
    {
        public string Username { get; private set; }
        public string AuthServer { get; private set; }

        private TextBox _txtUsername;
        private TextBox _txtAuthServer;

        public AuthlibInputDialog()
        {
            Title = "外置登录";
            Width = 420;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;

            var border = new Border
            {
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2D2D2D")),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.9, 0.9)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });

            var lblTitle = new TextBlock
            {
                Text = "外置登录 (Authlib-Injector)",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lblTitle, 0);
            grid.Children.Add(lblTitle);

            var lblUser = new TextBlock
            {
                Text = "玩家名称",
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(lblUser, 1);
            grid.Children.Add(lblUser);

            _txtUsername = new TextBox
            {
                FontSize = 14,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                CaretBrush = Brushes.White,
                Height = 36
            };
            Grid.SetRow(_txtUsername, 1);
            grid.Children.Add(_txtUsername);

            var lblServer = new TextBlock
            {
                Text = "认证服务器地址",
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(lblServer, 3);
            grid.Children.Add(lblServer);

            _txtAuthServer = new TextBox
            {
                Text = "https://auth.example.com/api/yggdrasil",
                FontSize = 14,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                CaretBrush = Brushes.White,
                Height = 36
            };
            Grid.SetRow(_txtAuthServer, 3);
            grid.Children.Add(_txtAuthServer);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, ev) => { DialogResult = false; Close(); };

            var btnOk = new Button
            {
                Content = "添加",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#9B59B6")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, ev) =>
            {
                Username = _txtUsername.Text;
                AuthServer = _txtAuthServer.Text;
                DialogResult = true;
                Close();
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 4);
            grid.Children.Add(btnPanel);

            border.Child = grid;
            Content = border;

            Loaded += (s, ev) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleX = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleY = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(Window.OpacityProperty, fadeIn);
                var st = (ScaleTransform)((Border)Content).RenderTransform;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                _txtUsername.Focus();
            };
        }
    }

    #endregion

    #region 微软授权码对话框

    /// <summary>
    /// 微软授权码登录对话框 —— 用户粘贴浏览器回调 URL
    /// </summary>
    public class AuthCodeDialog : Window
    {
        public event Func<string, bool> OnSubmitCode;
        public event Action OnCancel;

        private TextBlock _lblStatus;
        private TextBox _txtUrl;
        private Button _btnConfirm;

        public AuthCodeDialog()
        {
            Title = "微软登录";
            Width = 520;
            Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;

            var border = new Border
            {
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2D2D2D")),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.9, 0.9)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题
            var lblTitle = new TextBlock
            {
                Text = "登录 Microsoft 账号",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(lblTitle, 0);
            grid.Children.Add(lblTitle);

            // 说明
            var lblDesc = new TextBlock
            {
                Text = "已在浏览器中打开微软登录页面。\n登录完成后，将浏览器地址栏的完整 URL 粘贴到下方：",
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(lblDesc, 2);
            grid.Children.Add(lblDesc);

            // URL 输入框
            _txtUrl = new TextBox
            {
                Height = 32,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#555555")),
                BorderThickness = new Thickness(1),
                CaretBrush = Brushes.White,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0)
            };
            Grid.SetRow(_txtUrl, 4);
            grid.Children.Add(_txtUrl);

            // 状态文字
            _lblStatus = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#888888")),
                FontFamily = new FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(_lblStatus, 6);
            grid.Children.Add(_lblStatus);

            // 按钮
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, ev) =>
            {
                OnCancel?.Invoke();
                Close();
            };

            _btnConfirm = new Button
            {
                Content = "✓ 验证登录",
                Width = 120,
                Height = 36,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2196F3")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            _btnConfirm.Click += (s, ev) =>
            {
                string url = _txtUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    SetStatus("请先粘贴浏览器地址栏的完整 URL", false);
                    return;
                }

                _btnConfirm.IsEnabled = false;
                if (OnSubmitCode != null)
                {
                    bool ok = OnSubmitCode(url);
                    if (!ok)
                        _btnConfirm.IsEnabled = true;
                }
                else
                {
                    _btnConfirm.IsEnabled = true;
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(_btnConfirm);
            Grid.SetRow(btnPanel, 8);
            grid.Children.Add(btnPanel);

            border.Child = grid;
            Content = border;

            // 入场动画
            Loaded += (s, ev) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleX = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleY = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(Window.OpacityProperty, fadeIn);
                var st = (ScaleTransform)((Border)Content).RenderTransform;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

                _txtUrl.Focus();
            };

            Closed += (s, ev) => OnCancel?.Invoke();
        }

        public void SetStatus(string text, bool isSuccess)
        {
            _lblStatus.Text = text;
            _lblStatus.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    isSuccess ? "#4CAF50" : "#F44336"));

            if (!isSuccess)
                _btnConfirm.IsEnabled = true;
        }
    }

    #endregion
}
