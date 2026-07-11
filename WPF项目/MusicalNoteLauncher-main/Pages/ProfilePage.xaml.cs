﻿﻿using System;
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

        /// <summary>
        /// 当用户修改头像时触发，供外部（如 MainWindow）同步更新头像显示
        /// </summary>
        public static event Action OnAvatarChanged;

        public ProfilePage()
        {
            InitializeComponent();
            Loaded += ProfilePage_Loaded;
        }

        public ProfilePage(string username, bool isOfflineMode) : this()
        {
            txtQingniaoName.Text = MusicalNoteLauncher.AppContext.QingniaoName ?? username;
            txtUsername.Text = username;
            txtLoginMode.Text = isOfflineMode ? "离线模式" : "正版模式";
            txtQingniaoId.Text = MusicalNoteLauncher.AppContext.QingniaoId ?? "0000000000";
            // 青鸟账号页使用用户自定义头像（AvatarImage），而非 MC 皮肤头部立雕
            if (imgQingniaoHead != null)
                imgQingniaoHead.Source = GetDefaultAvatarImage();
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            AccountListControl.ItemsSource = _gameAccounts;
            // 兜底：确保有一个默认头像图片展示
            if (imgQingniaoHead != null && imgQingniaoHead.Source == null)
                imgQingniaoHead.Source = GetDefaultAvatarImage();
            // 初始化青鸟ID显示
            txtQingniaoId.Text = MusicalNoteLauncher.AppContext.QingniaoId ?? "--";
            txtQingniaoName.Text = MusicalNoteLauncher.AppContext.QingniaoName ?? MusicalNoteLauncher.AppContext.Username ?? "Player";
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
            RefreshQingNiaoInfo();
        }

        /// <summary>
        /// 刷新青鸟账号详情页的显示数据
        /// </summary>
        private void RefreshQingNiaoInfo()
        {
            bool isQingNiao = _selectedAccount != null && _selectedAccount.Type == AccountType.QingNiao;

            // 切换显示面板
            if (panelQingNiaoLoggedIn != null)
                panelQingNiaoLoggedIn.Visibility = isQingNiao ? Visibility.Visible : Visibility.Collapsed;
            if (panelQingNiaoNotLoggedIn != null)
                panelQingNiaoNotLoggedIn.Visibility = isQingNiao ? Visibility.Collapsed : Visibility.Visible;

            // 已登录时刷新数据
            if (isQingNiao)
            {
                txtQingniaoName.Text = AppContext.QingniaoName ?? AppContext.Username ?? "Player";
                txtUsername.Text = AppContext.Username ?? "Player";
                txtQingniaoId.Text = AppContext.QingniaoId ?? "--";
                txtLoginMode.Text = "青鸟模式";

                if (panelQingNiaoActions != null)
                {
                    panelQingNiaoActions.Visibility = Visibility.Visible;
                    string server = AccountManager.GetQingNiaoServer();
                    txtQingNiaoServer.Text = string.IsNullOrEmpty(server) ? "未设置" : "青鸟官方服务器";
                }

                if (imgQingniaoHead != null && imgQingniaoHead.Source == null)
                    imgQingniaoHead.Source = GetDefaultAvatarImage();
            }
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

                // 加载青鸟账号
                foreach (var record in AccountManager.GetServerLoginRecords("QingNiao"))
                {
                    var account = new GameAccount
                    {
                        Name = record.Item1,
                        Type = AccountType.QingNiao,
                        AuthServer = AccountManager.GetQingNiaoServer(),
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
                     a.Type == AccountType.AuthlibInjector && currentType == "Auth" ||
                     a.Type == AccountType.QingNiao && currentType == "QingNiao"));
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

        /// <summary>编辑青鸟显示名称</summary>
        private void BtnEditQingniaoName_Click(object sender, RoutedEventArgs e)
        {
            string currentName = txtQingniaoName.Text ?? "";

            // 内联输入弹窗
            var inputWindow = new Window
            {
                Title = "修改青鸟名字",
                Width = 360, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Transparent
            };

            var textPri = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var surfaceBg = TryFindResource("SurfaceBrush") as Brush ?? Brushes.Gray;
            var cardBg = TryFindResource("CardBackgroundBrush") as Brush ?? Brushes.Black;
            var borderBr = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
            var primaryBr = TryFindResource("PrimaryBrush") as Brush ?? Brushes.Blue;

            var root = new Border { Background = cardBg, CornerRadius = new CornerRadius(12), BorderBrush = borderBr, BorderThickness = new Thickness(1), Margin = new Thickness(4) };
            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock { Text = "修改青鸟名字", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = textPri, FontFamily = new FontFamily("Microsoft YaHei"), Margin = new Thickness(0, 0, 0, 12) });

            var txtInput = new TextBox { Text = currentName, FontSize = 14, Padding = new Thickness(10, 8, 10, 8), Background = surfaceBg, Foreground = textPri, BorderBrush = borderBr, BorderThickness = new Thickness(1), FontFamily = new FontFamily("Microsoft YaHei"), Margin = new Thickness(0, 0, 0, 14) };
            txtInput.SelectAll();
            txtInput.Focus();
            stack.Children.Add(txtInput);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "取消", FontSize = 13, Padding = new Thickness(18, 7, 18, 7), Foreground = textPri, Background = Brushes.Transparent, BorderBrush = borderBr, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), FontFamily = new FontFamily("Microsoft YaHei") };
            cancelBtn.Style = CreateRoundedButtonStyle(cancelBtn, Brushes.Transparent, borderBr);
            cancelBtn.Click += (s2, ev2) => { inputWindow.DialogResult = false; inputWindow.Close(); };
            btnRow.Children.Add(cancelBtn);

            var okBtn = new Button { Content = "确定", FontSize = 13, Padding = new Thickness(18, 7, 18, 7), Foreground = Brushes.White, Background = primaryBr, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontFamily = new FontFamily("Microsoft YaHei") };
            okBtn.Style = CreateRoundedButtonStyle(okBtn, primaryBr, null);
            okBtn.Click += (s2, ev2) => { inputWindow.Tag = txtInput.Text; inputWindow.DialogResult = true; inputWindow.Close(); };
            btnRow.Children.Add(okBtn);
            stack.Children.Add(btnRow);
            root.Child = stack;
            inputWindow.Content = root;

            // 回车确认
            txtInput.KeyDown += (s2, ev2) => { if (ev2.Key == System.Windows.Input.Key.Enter) { inputWindow.Tag = txtInput.Text; inputWindow.DialogResult = true; inputWindow.Close(); } };

            if (inputWindow.ShowDialog() == true)
            {
                string newName = (inputWindow.Tag as string)?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (newName.Length < 2 || newName.Length > 24)
                    {
                        ModernMessageBox.ShowWarning("名字长度应在2-24个字符之间", "提示");
                        return;
                    }
                    AppContext.SetQingniaoName(newName);
                    txtQingniaoName.Text = newName;
                }
            }
        }

        /// <summary>创建圆角按钮 Style</summary>
        private static Style CreateRoundedButtonStyle(Button btn, Brush bg, Brush border)
        {
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var borderElem = new FrameworkElementFactory(typeof(Border));
            borderElem.Name = "border";
            borderElem.SetValue(Border.BackgroundProperty, bg);
            borderElem.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            if (border != null) borderElem.SetValue(Border.BorderBrushProperty, border);
            borderElem.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderElem.SetValue(Border.PaddingProperty, btn.Padding);
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderElem.AppendChild(contentPresenter);
            template.VisualTree = borderElem;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, border, "border"));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, border, "border"));
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

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
                    DeleteCustomCape(account);
                    PCL.Account.Settings.Set($"SkinSlim_{account.Uuid}", dialog.IsSlim);
                }
                else if (!string.IsNullOrEmpty(dialog.SkinFilePath))
                {
                    SaveCustomSkin(account, dialog.SkinFilePath, dialog.IsSlim);
                    // 保存披风 (如果有)
                    if (!string.IsNullOrEmpty(dialog.CapeFilePath) && File.Exists(dialog.CapeFilePath))
                    {
                        SaveCustomCape(account, dialog.CapeFilePath);
                    }
                    else
                    {
                        DeleteCustomCape(account);
                    }
                }

                LoadAccountHeadImage(account);
            }
        }


        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            // 允许用户选择本地图片作为自定义头像
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择头像图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                string uid = MusicalNoteLauncher.AppContext.QingniaoId ?? "player";

                // avatars 目录
                string avatarsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "avatars");
                if (!Directory.Exists(avatarsDir))
                    Directory.CreateDirectory(avatarsDir);

                string targetFile = Path.Combine(avatarsDir, $"{uid}.png");

                // 方法 A：直接拷贝源文件到目标位置（若源是 PNG 可直接拷贝，否则解码后再编码成 PNG）
                string ext = Path.GetExtension(dlg.FileName)?.ToLower() ?? "";
                if (ext == ".png")
                {
                    // PNG 直接拷贝，避免任何解码/重新编码问题
                    File.Copy(dlg.FileName, targetFile, overwrite: true);
                }
                else
                {
                    // 其他格式：解码 → 重新编码为 PNG
                    var bmp = new BitmapImage();
                    using (var src = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = src;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    using (var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write))
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(bmp));
                        enc.Save(fs);
                    }
                }

                // 从刚刚写入的文件加载 BitmapImage（保证下次启动也能正常加载）
                var avatar = new BitmapImage();
                using (var fs = new FileStream(targetFile, FileMode.Open, FileAccess.Read))
                {
                    avatar.BeginInit();
                    avatar.CacheOption = BitmapCacheOption.OnLoad;
                    avatar.StreamSource = fs;
                    avatar.EndInit();
                    avatar.Freeze();
                }

                // 设置到 UI
                imgQingniaoHead.Source = avatar;

                // 同步到当前选中账号（若存在）
                var current = _gameAccounts?.FirstOrDefault(a => a.IsSelected);
                if (current != null)
                    current.AvatarImage = avatar;

                // 通知外部（如 MainWindow）更新头像显示
                OnAvatarChanged?.Invoke();
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("头像加载失败：" + ex.Message, "提示");
            }
        }

        /// <summary>生成玩家头像（优先从 avatars/{username}.png 读取，否则生成友好的默认头像）。</summary>
        public static BitmapImage GetDefaultAvatarImage()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string uid = MusicalNoteLauncher.AppContext.QingniaoId ?? "player";
                string avatarFile = Path.Combine(exeDir, "avatars", uid + ".png");

                // 已有自定义头像 → 直接加载
                if (File.Exists(avatarFile))
                {
                    var bmp = new BitmapImage();
                    using (var fs = new FileStream(avatarFile, FileMode.Open, FileAccess.Read))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = fs;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    return bmp;
                }
            }
            catch { }

            // 兜底：在内存里生成一个漂亮的默认头像（紫蓝渐变圆形 + 中心 "♪" 音符符号）
            return BuildDefaultAvatarBitmap();
        }

        /// <summary>生成默认头像（复用 MainWindow 的构建方法，保持一致性）。</summary>
        private static BitmapImage BuildDefaultAvatarBitmap()
        {
            return MainWindow.BuildDefaultAvatar();
        }

        private void BtnSwitchAccount_Click(object sender, RoutedEventArgs e)
        {
            NavigateToGameAccount();
        }

        private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
        {
            ModernMessageBox.ShowInfo("快速开始功能开发中...", "提示");
        }

        private void BtnBrowseMods_Click(object sender, RoutedEventArgs e)
        {
            ModernMessageBox.ShowInfo("浏览模组功能开发中...", "提示");
        }

        private void BtnGameSettings_Click(object sender, RoutedEventArgs e)
        {
            ModernMessageBox.ShowInfo("游戏设置功能开发中...", "提示");
        }

        /// <summary>
        /// 动态添加青鸟账号面板的快捷操作卡片（预留扩展）
        /// </summary>
        private void AddQingNiaoFeatureCards()
        {
            // 预留：可在运行时动态添加更多快捷操作按钮
        }

        /// <summary>
        /// 青鸟账号刷新登录 —— 重新弹出登录窗口进行认证
        /// </summary>
        private void BtnQingNiaoRefreshLogin_Click(object sender, RoutedEventArgs e)
        {
            DoQingNiaoLogin();
        }

        /// <summary>
        /// 未登录状态下点击"登录青鸟账号"按钮
        /// </summary>
        private void BtnQingNiaoLogin_Click(object sender, RoutedEventArgs e)
        {
            DoQingNiaoLogin();
        }

        /// <summary>
        /// 弹出青鸟登录窗口，成功后更新页面
        /// </summary>
        private void DoQingNiaoLogin()
        {
            var dialog = new MusicalNoteLauncher.Windows.QingNiaoLoginDialog();
            dialog.Owner = Window.GetWindow(this);

            dialog.OnLoginSuccess += (result) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // 更新当前账号
                    AppContext.Username = result.UserName;

                    // 添加到账号列表
                    var existing = _gameAccounts.FirstOrDefault(a =>
                        a.Type == AccountType.QingNiao && a.Name == result.UserName);
                    if (existing == null)
                    {
                        var account = new GameAccount
                        {
                            Name = result.UserName,
                            Type = AccountType.QingNiao,
                            AuthServer = result.ServerUrl,
                            Uuid = result.Uuid
                        };
                        LoadAccountHeadImage(account);
                        _gameAccounts.Insert(0, account);
                        SelectAccount(account);
                    }
                    else
                    {
                        existing.AuthServer = result.ServerUrl;
                        existing.Uuid = result.Uuid;
                        LoadAccountHeadImage(existing);
                        SelectAccount(existing);
                    }

                    // 刷新青鸟页面
                    RefreshQingNiaoInfo();
                    ModernMessageBox.ShowInfo("青鸟账号刷新成功！", "提示");
                });
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// 登出青鸟账号
        /// </summary>
        private void BtnQingNiaoLogout_Click(object sender, RoutedEventArgs e)
        {
            if (ModernMessageBox.ShowYesNo("确定要登出青鸟账号吗？\n登出后需重新登录才能使用。", "确认登出"))
            {
                // 清除青鸟相关缓存
                AccountManager.ExitQingNiaoLogin();
                AccountManager.RemoveAccount("QingNiao", AppContext.Username);

                // 从列表中移除青鸟账号
                var qingNiaoAccounts = _gameAccounts.Where(a => a.Type == AccountType.QingNiao).ToList();
                foreach (var acc in qingNiaoAccounts)
                    _gameAccounts.Remove(acc);

                // 切换到离线模式
                AppContext.Username = "Player";
                AppContext.IsOfflineMode = true;
                AccountManager.SetLoginType(McLoginType.Legacy);

                SelectAccount(null);
                RefreshQingNiaoInfo();

                ModernMessageBox.ShowInfo("已登出青鸟账号", "提示");
            }
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
                    ModernMessageBox.ShowInfo("玩家名称不能为空", "提示");
                    return;
                }
                if (name.Length < 3 || name.Length > 16)
                {
                    ModernMessageBox.ShowInfo("用户名长度需要在3-16个字符之间", "提示");
                    return;
                }
                if (_gameAccounts.Any(a => a.Type == AccountType.Offline && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    ModernMessageBox.ShowInfo("已存在同名离线账号", "提示");
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

                    ModernMessageBox.ShowInfo($"微软账号 {result.UserName} 登录成功！", "登录成功");
                }
            }
            catch (MsAuthException ex)
            {
                ModernMessageBox.ShowError(ex.Message, "登录失败");
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("登录失败: " + ex.Message, "错误");
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
                    ModernMessageBox.ShowInfo("玩家名称和认证服务器地址不能为空", "提示");
                    return;
                }
                if (_gameAccounts.Any(a => a.Type == AccountType.AuthlibInjector &&
                    a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    a.AuthServer == server))
                {
                    ModernMessageBox.ShowInfo("已存在相同的外置登录账号", "提示");
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
                if (ModernMessageBox.ShowYesNo(
                    $"确定要删除账号 \"{account.Name}\" 吗？\n此操作不可撤销。",
                    "确认删除"))
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
                        case AccountType.QingNiao:
                            loginType = "QingNiao";
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
                MusicalNoteLauncher.AppContext.CurrentAccountUuid = account.Uuid;

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
                    case AccountType.QingNiao:
                        loginType = McLoginType.Auth;
                        break;
                    default:
                        loginType = McLoginType.Legacy;
                        break;
                }
                AccountManager.SetLoginType(loginType);

                // 同步到青鸟账号页
                txtQingniaoName.Text = MusicalNoteLauncher.AppContext.QingniaoName ?? account.Name;
                txtUsername.Text = account.Name;
                txtLoginMode.Text = account.TypeDisplay + "模式";
                txtQingniaoId.Text = MusicalNoteLauncher.AppContext.QingniaoId ?? "0000000000";
                if (imgQingniaoHead != null)
                    imgQingniaoHead.Source = account.AvatarImage ?? GetDefaultAvatarImage();

                // 通知全局账号变更
                MusicalNoteLauncher.AppContext.NotifyAccountChanged(
                    account.Name, account.Uuid ?? "", account.Type != AccountType.Offline);

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

        private void SaveCustomCape(GameAccount account, string sourceFile)
        {
            string skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
            if (!Directory.Exists(skinsDir))
                Directory.CreateDirectory(skinsDir);

            string destFile = Path.Combine(skinsDir, $"{account.Uuid}_cape.png");
            try
            {
                File.Copy(sourceFile, destFile, true);
            }
            catch { }
        }

        private void DeleteCustomCape(GameAccount account)
        {
            string capeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins", $"{account.Uuid}_cape.png");
            try
            {
                if (File.Exists(capeFile))
                    File.Delete(capeFile);
            }
            catch { }
        }

        /// <summary>
        /// 从皮肤文件中提取玩家头部正面立雕（内层+外层叠加）。
        /// Minecraft 64×64 皮肤格式：
        ///   内层头部正面：x=8..15, y=8..15（玩家脸部）
        ///   外层头部正面：x=40..47, y=8..15（头饰/帽子，透明处透出内层）
        /// </summary>
        private void LoadAccountHeadImage(GameAccount account)
        {
            try
            {
                string skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");

                // 优先尝试 {uuid}.png，其次尝试 {username}.png，确保找到真正的皮肤文件
                string[] candidates = {
                    Path.Combine(skinsDir, $"{account.Uuid}.png"),
                    Path.Combine(skinsDir, $"{account.Name}.png")
                };

                string skinFile = null;
                foreach (var c in candidates)
                    if (File.Exists(c)) { skinFile = c; break; }

                if (skinFile != null)
                {
                    using (var fs = new FileStream(skinFile, FileMode.Open, FileAccess.Read))
                    {
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var rawFrame = decoder.Frames[0];

                        // 检测是否为 Minecraft 皮肤格式：
                        //   - 正方形 (1:1)：64×64 / 128×128 / 256×256 ...
                        //   - 宽高比 2:1：64×32 / 128×64 / 256×128 ...
                        // 不要求宽度是 64 的整数倍，支持非标准尺寸（例如 100×100 的自定义皮肤）
                        int w = rawFrame.PixelWidth;
                        int h = rawFrame.PixelHeight;
                        bool looksLikeSkin =
                            (w >= 48) && (h >= 16) &&
                            (w == h || w == 2 * h);

                        // 统一转为 Bgra32
                        var formatted = new FormatConvertedBitmap(rawFrame, PixelFormats.Bgra32, null, 0);
                        formatted.Freeze();

                        if (looksLikeSkin)
                            account.HeadImage = ComposeHeadFront(formatted);   // 皮肤 → 提取头部
                        else
                            account.HeadImage = formatted;                     // 普通图片 → 直接展示原图
                    }
                }
                else
                {
                    // 没有皮肤文件 → 默认 Steve/Alex
                    bool isSlim = false;
                    try { isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{account.Uuid}"); } catch { }
                    account.HeadImage = GenerateDefaultHead(isSlim);
                }
            }
            catch
            {
                // 兜底：直接返回默认头像
                try { account.HeadImage = GenerateDefaultHead(false); } catch { }
            }
        }

        /// <summary>
        /// 从 Minecraft 皮肤中提取头部正面，组合内层脸部和外层帽子后像素化放大到 32×32。
        /// 使用浮点缩放系数，支持标准 64×64、旧格式 64×32，以及任意 HD 皮肤尺寸（128×128、256×256 等）。
        /// 内层脸部：皮肤坐标 x=8..16, y=8..16 的 8×8 区域
        /// 外层帽子：皮肤坐标 x=40..48, y=8..16 的 8×8 区域（非透明像素覆盖在脸部上）
        /// </summary>
        private BitmapSource ComposeHeadFront(BitmapSource skin)
        {
            const int headSize = 8;
            const int outSize = 32;

            int w = skin.PixelWidth;
            int h = skin.PixelHeight;
            double scale = w / 64.0;  // 浮点缩放（64宽=1，128宽=2，100宽=1.5625）

            byte[] src = GetRawPixels(skin);
            var head = new byte[headSize * headSize * 4];

            // 1. 提取内层脸部正面（逻辑坐标 x=8..15, y=8..15，按 scale 映射到实际像素）
            //    每个逻辑像素取其中心点的实际像素
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sx = (int)Math.Floor(8.0 * scale + (x + 0.5) * scale);
                    int sy = (int)Math.Floor(8.0 * scale + (y + 0.5) * scale);
                    sx = Math.Max(0, Math.Min(sx, w - 1));
                    sy = Math.Max(0, Math.Min(sy, h - 1));
                    int si = (sy * w + sx) * 4;
                    int di = (y * headSize + x) * 4;
                    head[di + 0] = src[si + 0];
                    head[di + 1] = src[si + 1];
                    head[di + 2] = src[si + 2];
                    head[di + 3] = src[si + 3];
                }

            // 2. 外层帽子（逻辑坐标 x=40..47, y=8..15）—— 非透明像素覆盖内层
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sx = (int)Math.Floor(40.0 * scale + (x + 0.5) * scale);
                    int sy = (int)Math.Floor(8.0 * scale + (y + 0.5) * scale);
                    sx = Math.Max(0, Math.Min(sx, w - 1));
                    sy = Math.Max(0, Math.Min(sy, h - 1));
                    int si = (sy * w + sx) * 4;
                    if (src[si + 3] == 0) continue;  // 透明 → 保留内层脸部

                    int di = (y * headSize + x) * 4;
                    head[di + 0] = src[si + 0];
                    head[di + 1] = src[si + 1];
                    head[di + 2] = src[si + 2];
                    head[di + 3] = src[si + 3];
                }

            // 3. 8×8 → 32×32 像素化放大（最近邻 4×）
            var outPixels = new byte[outSize * outSize * 4];
            int ratio = outSize / headSize;
            for (int y = 0; y < outSize; y++)
                for (int x = 0; x < outSize; x++)
                {
                    int sx = x / ratio;
                    int sy = y / ratio;
                    int si = (sy * headSize + sx) * 4;
                    int di = (y * outSize + x) * 4;
                    outPixels[di + 0] = head[si + 0];
                    outPixels[di + 1] = head[si + 1];
                    outPixels[di + 2] = head[si + 2];
                    outPixels[di + 3] = head[si + 3];
                }

            var result = BitmapSource.Create(outSize, outSize, 96, 96, PixelFormats.Bgra32, null, outPixels, outSize * 4);
            result.Freeze();
            return result;
        }

        /// <summary>从 BitmapSource 提取 BGRA 原始像素数组</summary>
        private static byte[] GetRawPixels(BitmapSource src)
        {
            int w = src.PixelWidth;
            int stride = w * 4;
            var pixels = new byte[stride * src.PixelHeight];
            src.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        /// <summary>
        /// 生成默认头部立雕：从 Assets/Skins/steve.png 或 alex.png 加载（与 3D 预览同一皮肤源），
        /// 再提取头部正面（内层+外层）后像素化放大到 32×32。
        /// </summary>
        private BitmapSource GenerateDefaultHead(bool isSlim)
        {
            var skin = MusicalNoteLauncher.Core.DefaultSkinFactory.GetDefaultSkinBitmap(isSlim);
            return ComposeHeadFront(skin);
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
            return SkinServer.GenerateOfflineUuid(username);
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
                    ((SolidColorBrush)FindResource("CardHoverBrush")).Color),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    ((SolidColorBrush)FindResource("BorderBrush")).Color),
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
                    ((SolidColorBrush)FindResource("BackgroundBrush")).Color),
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
                    ((SolidColorBrush)FindResource("BackgroundBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
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
            return SkinServer.GenerateOfflineUuid(username);
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
                    ((SolidColorBrush)FindResource("CardHoverBrush")).Color),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    ((SolidColorBrush)FindResource("BorderBrush")).Color),
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
                    ((SolidColorBrush)FindResource("TextSecondaryBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
                BorderBrush = new SolidColorBrush(
                    ((SolidColorBrush)FindResource("BorderBrush")).Color),
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
                    ((SolidColorBrush)FindResource("TextSecondaryBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
                BorderBrush = new SolidColorBrush(
                    ((SolidColorBrush)FindResource("BorderBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
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

    #region 皮肤编辑对话框

    public class SkinEditDialog : Window
    {
        public string SkinFilePath { get; private set; }
        public string CapeFilePath { get; private set; }
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

        // 主题刷子（类成员，供构造函和辅助方法共用）
        private Brush _textPri;
        private Brush _textSec;
        private Brush _surfaceBg;
        private Brush _borderBr;
        private Brush _cardBg;

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

            _textPri = (Brush)FindResource("TextPrimaryBrush");
            _textSec = (Brush)FindResource("TextSecondaryBrush");
            _surfaceBg = (Brush)FindResource("SurfaceBrush");
            _borderBr = (Brush)FindResource("BorderBrush");
            _cardBg = (Brush)FindResource("CardBackgroundBrush");

            _currentSkinFile = existingSkinFile;
            _selectedSkinFile = existingSkinFile;
            _selectedCapeFile = null;
            IsSlim = isSlim;

            var rootBorder = new Border
            {
                Background = (Brush)Application.Current.FindResource("CardHoverBrush"),
                CornerRadius = new CornerRadius(16),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
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
                Foreground = _textPri,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            Grid.SetRow(title, 0);
            rootGrid.Children.Add(title);

            var bodyBorder = new Border
            {
                Background = (Brush)Application.Current.FindResource("CardBackgroundBrush"),
                CornerRadius = new CornerRadius(8),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
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
                Background = (Brush)Application.Current.FindResource("BackgroundBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(previewBorder, 0);
            var previewGrid = new Grid();
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var lblPreview = new TextBlock
            {
                Text = "3D 预览",
                Foreground = _textPri,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(lblPreview, 0);
            previewGrid.Children.Add(lblPreview);

            // 背景颜色切换按钮
            var bgSwatchPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            var bgColors = new[]
            {
                new { Color = (Color)ColorConverter.ConvertFromString("#2D2D2D"), Tip = "深灰" },
                new { Color = (Color)ColorConverter.ConvertFromString("#808080"), Tip = "灰色" },
                new { Color = (Color)ColorConverter.ConvertFromString("#C0C0C0"), Tip = "浅灰" },
                new { Color = (Color)ColorConverter.ConvertFromString("#F0F0F0"), Tip = "白色" },
                new { Color = (Color)ColorConverter.ConvertFromString("#00FF00"), Tip = "绿幕" },
                new { Color = (Color)ColorConverter.ConvertFromString("#4488FF"), Tip = "蓝色" }
            };
            foreach (var c in bgColors)
            {
                var swatch = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(c.Color),
                    BorderBrush = _borderBr,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2, 0, 2, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = c.Tip
                };
                var colorCopy = c.Color;
                swatch.MouseLeftButtonDown += (s, e) => _skin3dViewer.SetBackground(colorCopy);
                bgSwatchPanel.Children.Add(swatch);
            }
            Grid.SetRow(bgSwatchPanel, 1);
            previewGrid.Children.Add(bgSwatchPanel);

            _skin3dViewer = new Skin3DViewer();
            Grid.SetRow(_skin3dViewer, 2);
            previewGrid.Children.Add(_skin3dViewer);
            previewBorder.Child = previewGrid;
            bodyGrid.Children.Add(previewBorder);

            var rightBorder = new Border
            {
                Background = _surfaceBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(rightBorder, 2);
            var rightStack = new StackPanel();
            rightBorder.Child = rightStack;

            Action<RadioButton> rbStyle = (rb) =>
            {
                rb.Foreground = _textPri;
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
                Background = _surfaceBg,
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
                Foreground = _textSec,
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
                Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            var border_btnLittleLink = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = _borderBr,
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
                Background = _surfaceBg,
                Foreground = _textPri,
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            var border_btnCancel = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = _borderBr,
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
                BorderBrush = _borderBr,
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
                Foreground = _textSec,
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(lblModel);

            _modelCombo = new ComboBox
            {
                Background = _surfaceBg,
                Foreground = _textPri,
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
                Foreground = _textSec,
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(lblSkin);

            var skinRow = new StackPanel { Orientation = Orientation.Horizontal };
            _lblSkinFile = new TextBlock
            {
                Text = string.IsNullOrEmpty(_selectedSkinFile) ? "(未选择)" : System.IO.Path.GetFileName(_selectedSkinFile),
                Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
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
                Background = _surfaceBg,
                Foreground = _textPri,
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
                Foreground = _textSec,
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 10, 0, 4)
            };
            panel.Children.Add(lblCape);

            var capeRow = new StackPanel { Orientation = Orientation.Horizontal };
            _lblCapeFile = new TextBlock
            {
                Text = "(未选择)",
                Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
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
                Background = _surfaceBg,
                Foreground = _textPri,
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border_btnPickCape = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = _borderBr,
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
                Foreground = _textSec,
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
                Foreground = _textSec,
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(hint);

            var urlBox = new TextBox
            {
                Background = _surfaceBg,
                Foreground = _textPri,
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
            var bgBrush = (Brush)Application.Current.FindResource("SurfaceBrush");
            var fgBrush = (Brush)Application.Current.FindResource("TextPrimaryBrush");
            var borderBrush = (Brush)Application.Current.FindResource("BorderBrush");
            var cbBg = (Brush)Application.Current.FindResource("CardBackgroundBrush");

            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, bgBrush));
            style.Setters.Add(new Setter(Control.ForegroundProperty, fgBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, borderBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            style.Setters.Add(new Setter(ComboBox.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Left));
            style.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty,
                VerticalAlignment.Center));

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, cbBg));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, fgBrush));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            var itemHoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            itemHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                (Brush)Application.Current.FindResource("BorderBrush")));
            itemStyle.Triggers.Add(itemHoverTrigger);
            var itemSelectedTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            itemSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                (Brush)Application.Current.FindResource("CardHoverBrush")));
            itemStyle.Triggers.Add(itemSelectedTrigger);
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));

            var template = new ControlTemplate(typeof(ComboBox));

            var rootBorderFactory = new FrameworkElementFactory(typeof(Border), "templateRoot");
            rootBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            rootBorderFactory.SetValue(Border.BackgroundProperty, bgBrush);
            rootBorderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
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
            toggleButtonFactory.SetValue(Control.BackgroundProperty, bgBrush);
            toggleButtonFactory.SetValue(Control.ForegroundProperty, fgBrush);
            toggleButtonFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            toggleButtonFactory.SetValue(Control.PaddingProperty, new Thickness(4, 0, 4, 0));
            toggleButtonFactory.SetValue(FrameworkElement.MinWidthProperty, 18.0);
            toggleButtonFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetValue(Border.BackgroundProperty, bgBrush);
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 4, 4, 0));
            toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            toggleTemplate.VisualTree = toggleBorder;

            var pathFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            pathFactory.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
            pathFactory.SetValue(System.Windows.Shapes.Shape.FillProperty, fgBrush);
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
                (Brush)Application.Current.FindResource("CardBackgroundBrush"));
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
            return MusicalNoteLauncher.Core.DefaultSkinFactory.GetDefaultSkinBitmap(isSlim);
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
                        if (!ModernMessageBox.ShowYesNo(
                            "所选图片不是标准尺寸。仍然继续使用吗？",
                            "提示"))
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
                CapeFilePath = null;
            }
            else if (_currentSource == SkinSource.LocalFile)
            {
                IsDefault = false;
                if (string.IsNullOrEmpty(_selectedSkinFile) || !File.Exists(_selectedSkinFile))
                {
                    ModernMessageBox.ShowWarning("请选择有效的皮肤文件", "提示");
                    return;
                }
                SkinFilePath = _selectedSkinFile;
                CapeFilePath = _selectedCapeFile;
            }
            else
            {
                IsDefault = false;
                SkinFilePath = null;
                CapeFilePath = null;
                ModernMessageBox.ShowInfo("请选择默认或本地文件来应用皮肤", "提示");
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
                    ((SolidColorBrush)FindResource("CardHoverBrush")).Color),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(
                    ((SolidColorBrush)FindResource("BorderBrush")).Color),
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
                    ((SolidColorBrush)FindResource("TextSecondaryBrush")).Color),
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
                    ((SolidColorBrush)FindResource("BackgroundBrush")).Color),
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
                    ((SolidColorBrush)FindResource("SurfaceBrush")).Color),
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
                    ((SolidColorBrush)FindResource("PrimaryBrush")).Color),
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
