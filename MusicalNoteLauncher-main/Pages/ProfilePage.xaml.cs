using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;
using PCL.Account;
using PCL.Auth.Microsoft;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

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

            try
            {
                // 加载离线账号
                foreach (string name in AccountManager.GetLegacyAccounts())
                {
                    var account = new GameAccount
                    {
                        Name = name,
                        Type = AccountType.Offline,
                        Uuid = GenerateOfflineUuid(name)
                    };
                    LoadAccountHeadImage(account);
                    _gameAccounts.Add(account);
                }

                // 加载微软账号
                foreach (var ms in AccountManager.GetMsAccounts())
                {
                    var account = new GameAccount
                    {
                        Name = ms.UserName,
                        Type = AccountType.Microsoft,
                        Uuid = ms.Uuid ?? GenerateOfflineUuid(ms.UserName)
                    };
                    LoadAccountHeadImage(account);
                    _gameAccounts.Add(account);
                }

                // 加载外置登录账号
                foreach (var record in AccountManager.GetServerLoginRecords("Auth"))
                {
                    var account = new GameAccount
                    {
                        Name = record.Item1,
                        Type = AccountType.AuthlibInjector,
                        AuthServer = AccountManager.GetAuthServer(),
                        Uuid = GenerateOfflineUuid(record.Item1)
                    };
                    LoadAccountHeadImage(account);
                    _gameAccounts.Add(account);
                }
            }
            catch (Exception ex)
            {
                // 即使加载头像失败也应显示账号卡片
                System.Diagnostics.Debug.WriteLine("LoadGameAccounts error: " + ex.Message);
            }

            // 恢复当前选中状态
            try
            {
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
            catch { }
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

        private void BtnEditSkin_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.Tag is GameAccount account)) return;

            bool currentSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{account.Uuid}");
            string currentSkinFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins", $"{account.Uuid}.png");
            bool hasCustomSkin = File.Exists(currentSkinFile);

            var dialog = new SkinEditDialog(account.Name, hasCustomSkin ? currentSkinFile : null, currentSlim);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                if (dialog.IsDefault)
                {
                    DeleteCustomSkin(account);
                    PCL.Account.Settings.Set($"SkinSlim_{account.Uuid}", dialog.IsSlim);
                }
                else if (!string.IsNullOrEmpty(dialog.SkinFilePath))
                {
                    SaveCustomSkin(account, dialog.SkinFilePath, dialog.IsSlim);
                }

                LoadAccountHeadImage(account);
            }
        }


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
                LoadAccountHeadImage(account);
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

                    LoadAccountHeadImage(account);
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
                LoadAccountHeadImage(account);
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


        private void SaveCustomSkin(GameAccount account, string sourceFile, bool isSlim)
        {
            string skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
            if (!Directory.Exists(skinsDir))
                Directory.CreateDirectory(skinsDir);

            string destFile = Path.Combine(skinsDir, $"{account.Uuid}.png");

            // 先把源文件完整读进内存流，彻底释放文件句柄
            // 这样既避免了 sourceFile == destFile 的自引用冲突，也避免了
            // 3D 预览控件中 ImageBrush/BitmapImage 持有文件句柄导致的 IOException
            byte[] rawData = File.ReadAllBytes(sourceFile);

            var decoder = BitmapDecoder.Create(
                new MemoryStream(rawData),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            if (frame.PixelWidth == 64 && frame.PixelHeight == 32)
            {
                var drawingVisual = new DrawingVisual();
                using (var ctx = drawingVisual.RenderOpen())
                {
                    ctx.DrawImage(frame, new Rect(0, 0, 64, 32));
                }
                var bmp = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    ms.Position = 0;
                    // 以覆盖方式写回目标文件（FileShare.ReadWrite 避免被自身占用）
                    using (var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        ms.CopyTo(fs);
                    }
                }
            }
            else
            {
                // 以覆盖方式写回目标文件（允许 FileShare.ReadWrite，避免被自身占用）
                using (var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.Write(rawData, 0, rawData.Length);
                }
            }

            PCL.Account.Settings.Set($"SkinSlim_{account.Uuid}", isSlim);
        }

        private void DeleteCustomSkin(GameAccount account)
        {
            string skinFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins", $"{account.Uuid}.png");
            try
            {
                if (File.Exists(skinFile))
                    File.Delete(skinFile);
            }
            catch { }
            PCL.Account.Settings.Set($"SkinSlim_{account.Uuid}", false);
        }

        /// <summary>
        /// 从皮肤文件中裁剪头部（8×8 区域，位于 64×64 皮肤的 x=8,y=8 处）
        /// </summary>
        private void LoadAccountHeadImage(GameAccount account)
        {
            try
            {
                string skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                string skinFile = Path.Combine(skinsDir, $"{account.Uuid}.png");

                if (File.Exists(skinFile))
                {
                    var decoder = BitmapDecoder.Create(
                        new Uri(skinFile), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var fullFrame = decoder.Frames[0];

                    int headX = 8, headY = 8, headSize = 8;
                    if (fullFrame.PixelWidth < headX + headSize || fullFrame.PixelHeight < headY + headSize)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(skinFile);
                        bmp.EndInit();
                        bmp.Freeze();
                        account.HeadImage = bmp;
                        return;
                    }

                    var cropped = new CroppedBitmap(fullFrame,
                        new Int32Rect(headX, headY, headSize, headSize));
                    var scaled = new TransformedBitmap(cropped, new ScaleTransform(8, 8));

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(scaled));
                    var ms = new MemoryStream();
                    encoder.Save(ms);
                    ms.Position = 0;

                    var result = new BitmapImage();
                    result.BeginInit();
                    result.CacheOption = BitmapCacheOption.OnLoad;
                    result.StreamSource = ms;
                    result.EndInit();
                    result.Freeze();

                    account.HeadImage = result;
                }
                else
                {
                    bool isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{account.Uuid}");
                    account.HeadImage = GenerateDefaultHead(isSlim);
                }
            }
            catch
            {
                account.HeadImage = null;
            }
        }

        /// <summary>
        /// 生成默认头部（8x8 肤色方块放大到 64x64）
        /// </summary>
        private BitmapImage GenerateDefaultHead(bool isSlim)
        {
            const int size = 8;
            var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int idx = (y * size + x) * 4;
                    pixels[idx + 0] = 75;
                    pixels[idx + 1] = 105;
                    pixels[idx + 2] = 141;
                    pixels[idx + 3] = 255;
                }
            wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

            var scaled = new TransformedBitmap(wb, new ScaleTransform(8, 8));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(scaled));
            var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            var result = new BitmapImage();
            result.BeginInit();
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.StreamSource = ms;
            result.EndInit();
            result.Freeze();
            return result;
        }        /// <summary>
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
        private void BtnRefreshAccount_Click(object sender, RoutedEventArgs e)
        {
            LoadGameAccounts();
        }

        private void BtnCopyUuid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameAccount acc)
            {
                try { Clipboard.SetText(acc.Uuid); } catch { }
            }
        }

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
            var border_btnCancel = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnCancel
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
            var border_btnOk = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnOk
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

            btnPanel.Children.Add(border_btnCancel);
            btnPanel.Children.Add(border_btnOk);
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
            var border_btnCancel = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnCancel
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
            var border_btnOk = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnOk
            };
            btnOk.Click += (s, ev) =>
            {
                Username = _txtUsername.Text;
                AuthServer = _txtAuthServer.Text;
                DialogResult = true;
                Close();
            };

            btnPanel.Children.Add(border_btnCancel);
            btnPanel.Children.Add(border_btnOk);
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

﻿    #region 皮肤编辑对话框

    public class SkinEditDialog : Window
    {
        public string SkinFilePath { get; private set; }
        public bool IsSlim { get; private set; }
        public bool IsDefault { get; private set; }

        private Skin3DViewer _skin3dViewer;

        private RadioButton _rbDefault;
        private RadioButton _rbSteve;
        private RadioButton _rbAlex;
        private RadioButton _rbLocalFile;
        private RadioButton _rbLittleSkin;
        private RadioButton _rbCslApi;

        private Border _dynamicPanel;
        private StackPanel _dynamicStack;

        private StackPanel _localFilePanel;
        private ComboBox _modelCombo;
        private TextBlock _lblSkinFile;
        private TextBlock _lblCapeFile;

        private TextBlock _lblStatus;

        private string _currentSkinFile;
        private string _selectedSkinFile;
        private string _selectedCapeFile;

        private enum SkinSource { Default, Steve, Alex, LocalFile, LittleSkin, CslApi }
        private SkinSource _currentSource;

        public SkinEditDialog(string accountName, string existingSkinFile, bool isSlim)
        {
            Title = "编辑皮肤 - " + accountName;
            Width = 640;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;

            _currentSkinFile = existingSkinFile;
            _selectedSkinFile = existingSkinFile;
            _selectedCapeFile = null;
            IsSlim = isSlim;

            var rootBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.9, 0.9)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "编辑皮肤 - " + accountName,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            Grid.SetRow(title, 0);
            rootGrid.Children.Add(title);

            var bodyBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525")),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12)
            };
            Grid.SetRow(bodyBorder, 2);
            rootGrid.Children.Add(bodyBorder);

            var bodyGrid = new Grid();
            // 640 - padding(20*2) - bodyBorder padding(12*2) = 576 可用宽度
            // 282 + 12 + 282 = 576
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(282) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(282) });
            bodyBorder.Child = bodyGrid;

            var previewBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(previewBorder, 0);
            var previewGrid = new Grid();
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var lblPreview = new TextBlock
            {
                Text = "3D 预览",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(lblPreview, 0);
            previewGrid.Children.Add(lblPreview);
            _skin3dViewer = new Skin3DViewer();
            Grid.SetRow(_skin3dViewer, 2);
            previewGrid.Children.Add(_skin3dViewer);
            previewBorder.Child = previewGrid;
            bodyGrid.Children.Add(previewBorder);

            var rightBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(rightBorder, 2);
            var rightStack = new StackPanel();
            rightBorder.Child = rightStack;

            Action<RadioButton> rbStyle = (rb) =>
            {
                rb.Foreground = Brushes.White;
                rb.FontFamily = new FontFamily("Microsoft YaHei");
                rb.FontSize = 13;
                rb.Margin = new Thickness(0, 0, 0, 8);
                rb.GroupName = "SkinSourceGroup";
            };

            bool hasExisting = !string.IsNullOrEmpty(existingSkinFile);

            _rbDefault = new RadioButton { Content = "默认", IsChecked = !hasExisting };
            rbStyle(_rbDefault);
            _rbDefault.Checked += (s, e) => SelectSource(SkinSource.Default);
            rightStack.Children.Add(_rbDefault);

            _rbSteve = new RadioButton { Content = "Steve", IsChecked = false };
            rbStyle(_rbSteve);
            _rbSteve.Checked += (s, e) => SelectSource(SkinSource.Steve);
            rightStack.Children.Add(_rbSteve);

            _rbAlex = new RadioButton { Content = "Alex", IsChecked = false };
            rbStyle(_rbAlex);
            _rbAlex.Checked += (s, e) => SelectSource(SkinSource.Alex);
            rightStack.Children.Add(_rbAlex);

            _rbLocalFile = new RadioButton { Content = "本地文件", IsChecked = hasExisting };
            rbStyle(_rbLocalFile);
            _rbLocalFile.Checked += (s, e) => SelectSource(SkinSource.LocalFile);
            rightStack.Children.Add(_rbLocalFile);

            _rbLittleSkin = new RadioButton { Content = "LittleSkin", IsChecked = false };
            rbStyle(_rbLittleSkin);
            _rbLittleSkin.Checked += (s, e) => SelectSource(SkinSource.LittleSkin);
            rightStack.Children.Add(_rbLittleSkin);

            _rbCslApi = new RadioButton { Content = "CSL API", IsChecked = false };
            rbStyle(_rbCslApi);
            _rbCslApi.Checked += (s, e) => SelectSource(SkinSource.CslApi);
            rightStack.Children.Add(_rbCslApi);

            _dynamicPanel = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = (_dynamicStack = new StackPanel())
            };
            rightStack.Children.Add(_dynamicPanel);

            _localFilePanel = BuildLocalFilePanel();
            var littleSkinPanel = BuildLittleSkinPanel();
            var cslPanel = BuildCslPanel();

            _dynamicStack.Children.Add(_localFilePanel);
            _dynamicStack.Children.Add(littleSkinPanel);
            _dynamicStack.Children.Add(cslPanel);

            _lblStatus = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                FontSize = 11,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            rightStack.Children.Add(_lblStatus);
            bodyGrid.Children.Add(rightBorder);

            var bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(bottomPanel, 4);
            rootGrid.Children.Add(bottomPanel);

            var btnLittleLink = new Button
            {
                Content = "LittleSkin",
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            var border_btnLittleLink = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnLittleLink
            };
            btnLittleLink.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("https://littleskin.cn"); }
                catch { }
            };
            bottomPanel.Children.Add(border_btnLittleLink);

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            var border_btnCancel = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnCancel
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            bottomPanel.Children.Add(border_btnCancel);

            var btnOk = new Button
            {
                Content = "确定",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            var border_btnOk = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnOk
            };
            btnOk.Click += (s, e) => ConfirmAndSave();
            bottomPanel.Children.Add(border_btnOk);

            rootBorder.Child = rootGrid;
            Content = rootBorder;

            Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var scaleX = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var scaleY = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                BeginAnimation(Window.OpacityProperty, fadeIn);
                var st = (ScaleTransform)((Border)Content).RenderTransform;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

                LoadCurrentPreview();
                if (hasExisting)
                    SelectSource(SkinSource.LocalFile);
                else
                    SelectSource(SkinSource.Default);
            };
        }

        private StackPanel BuildLocalFilePanel()
        {
            var panel = new StackPanel { Visibility = Visibility.Collapsed };

            var lblModel = new TextBlock
            {
                Text = "模型",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(lblModel);

            _modelCombo = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _modelCombo.Style = BuildDarkComboBoxStyle();
            _modelCombo.Items.Add("Steve (WIDE)");
            _modelCombo.Items.Add("Alex (SLIM)");
            _modelCombo.SelectedIndex = IsSlim ? 1 : 0;
            _modelCombo.SelectionChanged += (s, e) =>
            {
                IsSlim = (_modelCombo.SelectedIndex == 1);
                UpdatePreviewFromCurrent();
            };
            panel.Children.Add(_modelCombo);

            var lblSkin = new TextBlock
            {
                Text = "皮肤",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(lblSkin);

            var skinRow = new StackPanel { Orientation = Orientation.Horizontal };
            _lblSkinFile = new TextBlock
            {
                Text = string.IsNullOrEmpty(_selectedSkinFile) ? "(未选择)" : System.IO.Path.GetFileName(_selectedSkinFile),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 160
            };
            var btnPickSkin = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border_btnPickSkin = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnPickSkin
            };
            btnPickSkin.Click += (s, e) => PickSkinFile();
            skinRow.Children.Add(_lblSkinFile);
            skinRow.Children.Add(border_btnPickSkin);
            panel.Children.Add(skinRow);

            var lblCape = new TextBlock
            {
                Text = "披风",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 10, 0, 4)
            };
            panel.Children.Add(lblCape);

            var capeRow = new StackPanel { Orientation = Orientation.Horizontal };
            _lblCapeFile = new TextBlock
            {
                Text = "(未选择)",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 160
            };
            var btnPickCape = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border_btnPickCape = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnPickCape
            };
            btnPickCape.Click += (s, e) => PickCapeFile();
            capeRow.Children.Add(_lblCapeFile);
            capeRow.Children.Add(border_btnPickCape);
            panel.Children.Add(capeRow);

            return panel;
        }

        private StackPanel BuildLittleSkinPanel()
        {
            var panel = new StackPanel { Visibility = Visibility.Collapsed };
            var hint = new TextBlock
            {
                Text = "在 https://littleskin.cn 上上传并管理皮肤",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            panel.Children.Add(hint);
            return panel;
        }

        private StackPanel BuildCslPanel()
        {
            var panel = new StackPanel { Visibility = Visibility.Collapsed };
            var hint = new TextBlock
            {
                Text = "请输入 CSL API 基础 URL（当前版本仍需通过本地文件路径来应用皮肤）",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(hint);

            var urlBox = new TextBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Height = 28,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4),
                Text = "https://"
            };
            panel.Children.Add(urlBox);
            return panel;
        }

        private static Style BuildDarkComboBoxStyle()
        {
            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A"))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            style.Setters.Add(new Setter(ComboBox.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Left));
            style.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty,
                VerticalAlignment.Center));

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"))));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            var itemHoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            itemHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"))));
            itemStyle.Triggers.Add(itemHoverTrigger);
            var itemSelectedTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            itemSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A"))));
            itemStyle.Triggers.Add(itemSelectedTrigger);
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));

            var template = new ControlTemplate(typeof(ComboBox));

            var rootBorderFactory = new FrameworkElementFactory(typeof(Border), "templateRoot");
            rootBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            rootBorderFactory.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            rootBorderFactory.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")));
            rootBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            rootBorderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            template.VisualTree = rootBorderFactory;

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            rootBorderFactory.AppendChild(gridFactory);

            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            gridFactory.AppendChild(col0);

            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(0, GridUnitType.Auto));
            gridFactory.AppendChild(col1);

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter), "ContentPresenter");
            contentPresenterFactory.SetValue(Grid.ColumnProperty, 0);
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Left);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.MarginProperty, new Thickness(6, 0, 0, 0));
            contentPresenterFactory.SetValue(ContentPresenter.SnapsToDevicePixelsProperty, true);
            gridFactory.AppendChild(contentPresenterFactory);

            var toggleButtonFactory = new FrameworkElementFactory(typeof(ToggleButton), "DropDownToggle");
            toggleButtonFactory.SetValue(Grid.ColumnProperty, 1);
            toggleButtonFactory.SetValue(ToggleButton.FocusableProperty, false);
            toggleButtonFactory.SetValue(ToggleButton.IsCheckedProperty,
                new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            toggleButtonFactory.SetValue(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            toggleButtonFactory.SetValue(Control.ForegroundProperty, Brushes.White);
            toggleButtonFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            toggleButtonFactory.SetValue(Control.PaddingProperty, new Thickness(4, 0, 4, 0));
            toggleButtonFactory.SetValue(FrameworkElement.MinWidthProperty, 18.0);
            toggleButtonFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 4, 4, 0));
            toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            toggleTemplate.VisualTree = toggleBorder;

            var pathFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            pathFactory.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
            pathFactory.SetValue(System.Windows.Shapes.Shape.FillProperty, Brushes.White);
            pathFactory.SetValue(System.Windows.Shapes.Shape.StretchProperty, Stretch.None);
            pathFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 0));
            toggleBorder.AppendChild(pathFactory);

            toggleButtonFactory.SetValue(Control.TemplateProperty, toggleTemplate);
            gridFactory.AppendChild(toggleButtonFactory);

            var popupFactory = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popupFactory.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popupFactory.SetValue(Popup.StaysOpenProperty, false);
            popupFactory.SetValue(Popup.AllowsTransparencyProperty, true);
            popupFactory.SetValue(Popup.IsOpenProperty,
                new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            gridFactory.AppendChild(popupFactory);

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525")));
            popupBorder.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(0));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty,
                new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            popupFactory.AppendChild(popupBorder);

            var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewerFactory.SetValue(ScrollViewer.CanContentScrollProperty, true);
            popupBorder.AppendChild(scrollViewerFactory);

            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter), "ItemsPresenter");
            itemsPresenterFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            itemsPresenterFactory.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);
            scrollViewerFactory.AppendChild(itemsPresenterFactory);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }
        private static BitmapSource GenerateDefaultSkinBitmap(bool isSlim)
        {
            string skinFile = isSlim
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Skins", "alex.png")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Skins", "steve.png");

            if (File.Exists(skinFile))
            {
                try
                {
                    // 使用 BitmapDecoder + PreservePixelFormat 确保可靠的像素格式
                    var decoder = BitmapDecoder.Create(
                        new Uri(skinFile),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];

                    // 显式转换为 Bgra32，消除 BitmapImage 内部 Pbgra32/Bgra32 差异
                    BitmapSource bgraSource;
                    if (frame.Format == PixelFormats.Bgra32)
                        bgraSource = frame;
                    else
                        bgraSource = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

                    // 复制到 WriteableBitmap 确保像素数据完全可控
                    int w = bgraSource.PixelWidth;
                    int h = bgraSource.PixelHeight;
                    var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                    int stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    bgraSource.CopyPixels(pixels, stride, 0);
                    wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                    wb.Freeze();

                    // 诊断：保存加载的原始皮肤到输出目录，方便排查
                    try
                    {
                        string diagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                            isSlim ? "diag_alex_loaded.png" : "diag_steve_loaded.png");
                        using (var fs = new FileStream(diagPath, FileMode.Create))
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(wb));
                            encoder.Save(fs);
                        }
                    }
                    catch { }

                    return wb;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Skin3D] Failed to load default skin '{skinFile}': {ex.Message}");
                }
            }

            // Fallback：生成一个简单的 64x64 皮肤（仅头部和身体区域有颜色，其余白色）
            const int width = 64;
            const int height = 64;
            int fbStride = width * 4;
            byte[] fbPixels = new byte[fbStride * height];

            // 全部填充白色 (255,255,255,255)，与 MakeOpaque 行为一致
            for (int i = 0; i < fbPixels.Length; i += 4)
            {
                fbPixels[i] = 255;     // B
                fbPixels[i + 1] = 255; // G
                fbPixels[i + 2] = 255; // R
                fbPixels[i + 3] = 255; // A
            }

            System.Diagnostics.Debug.WriteLine($"[Skin3D] WARNING: Using fallback skin for {(isSlim ? "Alex" : "Steve")}");

            var fb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            fb.WritePixels(new Int32Rect(0, 0, width, height), fbPixels, fbStride, 0);
            fb.Freeze();
            return fb;
        }
        private void SelectSource(SkinSource source)
        {
            _currentSource = source;
            _localFilePanel.Visibility = (source == SkinSource.LocalFile) ? Visibility.Visible : Visibility.Collapsed;
            foreach (UIElement child in _dynamicStack.Children)
            {
                if (!ReferenceEquals(child, _localFilePanel))
                    child.Visibility = Visibility.Collapsed;
            }
            int idx = 0;
            foreach (UIElement child in _dynamicStack.Children)
            {
                if (idx == 1 && source == SkinSource.LittleSkin)
                    child.Visibility = Visibility.Visible;
                else if (idx == 2 && source == SkinSource.CslApi)
                    child.Visibility = Visibility.Visible;
                idx++;
            }

            switch (source)
            {
                case SkinSource.Default:
                case SkinSource.Steve:
                    IsDefault = true;
                    IsSlim = false;
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "将使用默认皮肤";
                    break;
                case SkinSource.Alex:
                    IsDefault = true;
                    IsSlim = true;
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "将使用Alex皮肤";
                    break;
                case SkinSource.LocalFile:
                    IsDefault = false;
                    UpdatePreviewFromCurrent();
                    break;
                case SkinSource.LittleSkin:
                case SkinSource.CslApi:
                    IsDefault = false;
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "请使用本地文件来应用皮肤";
                    break;
            }
        }

        private BitmapSource GetSkinBitmap()
        {
            if (string.IsNullOrEmpty(_selectedSkinFile))
                return null;
            if (!File.Exists(_selectedSkinFile))
                return null;
            try
            {
                byte[] raw = File.ReadAllBytes(_selectedSkinFile);
                var ms = new MemoryStream(raw);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private void UpdatePreviewFromCurrent()
        {
            if (string.IsNullOrEmpty(_selectedSkinFile) || !File.Exists(_selectedSkinFile))
            {
                if (_currentSource == SkinSource.Default ||
                    _currentSource == SkinSource.Steve ||
                    _currentSource == SkinSource.Alex)
                {
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "将使用默认皮肤";
                }
                else
                {
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "未选择皮肤文件";
                }
                return;
            }
            try
            {
                byte[] raw = File.ReadAllBytes(_selectedSkinFile);
                var ms = new MemoryStream(raw);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                _skin3dViewer.UpdateSkin(bmp, IsSlim);
                _lblStatus.Text = "已选择皮肤文件";
            }
            catch
            {
                _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                _lblStatus.Text = "无法加载皮肤文件";
            }
        }

        private void LoadCurrentPreview()
        {
            if (!string.IsNullOrEmpty(_currentSkinFile) && File.Exists(_currentSkinFile))
            {
                UpdatePreviewFromCurrent();
            }
            else
            {
                _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
            }
        }

        private void PickSkinFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择皮肤文件",
                Filter = "皮肤图片|*.png;*.jpg;*.jpeg|所有文件|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = dialog.FileName;
                    var decoder = BitmapDecoder.Create(
                        new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    if (decoder.Frames.Count == 0)
                    {
                        _lblStatus.Text = "所选文件不是有效的图片";
                        return;
                    }
                    var frame = decoder.Frames[0];
                    bool okSize = (frame.PixelWidth == 64 && (frame.PixelHeight == 32 || frame.PixelHeight == 64));
                    if (!okSize)
                    {
                        var res = MessageBox.Show(
                            "所选图片不是标准尺寸。仍然继续使用吗？",
                            "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (res != MessageBoxResult.Yes)
                            return;
                    }
                    _selectedSkinFile = filePath;
                    _lblSkinFile.Text = System.IO.Path.GetFileName(filePath);
                    _rbLocalFile.IsChecked = true;
                    UpdatePreviewFromCurrent();
                }
                catch (Exception ex)
                {
                    _lblStatus.Text = "加载皮肤失败：" + ex.Message;
                }
            }
        }

        private void PickCapeFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择披风文件",
                Filter = "披风图片|*.png;*.jpg;*.jpeg|所有文件|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                _selectedCapeFile = dialog.FileName;
                _lblCapeFile.Text = System.IO.Path.GetFileName(dialog.FileName);
                _lblStatus.Text = "已选择披风文件";
                _rbLocalFile.IsChecked = true;
            }
        }

        private void ConfirmAndSave()
        {
            if (_currentSource == SkinSource.Default ||
                _currentSource == SkinSource.Steve ||
                _currentSource == SkinSource.Alex)
            {
                IsDefault = true;
                SkinFilePath = null;
            }
            else if (_currentSource == SkinSource.LocalFile)
            {
                IsDefault = false;
                if (string.IsNullOrEmpty(_selectedSkinFile) || !File.Exists(_selectedSkinFile))
                {
                    MessageBox.Show("请选择有效的皮肤文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SkinFilePath = _selectedSkinFile;
            }
            else
            {
                IsDefault = false;
                SkinFilePath = null;
                MessageBox.Show("请选择默认或本地文件来应用皮肤", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
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
            var border_btnCancel = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                Child = btnCancel
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

            btnPanel.Children.Add(border_btnCancel);
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
