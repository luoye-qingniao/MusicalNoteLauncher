using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.Pages;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly LauncherCore _launcherCore;
        private readonly Dictionary<string, UserControl> _pageCache = new();
        private string _currentPageKey = "";
        private bool _activeIsA = true;
        private bool _isAnimating;
        public event Action OnLogout;

        // 背景性能：跟踪上次模式+路径，避免滑块拖动时重复加载文件
        private string _lastBgState = "";

        public MainWindow() : this("Player", true) { }

        public MainWindow(string username, bool isOfflineMode)
        {
            InitializeComponent();
            WindowEffectHelper.EnableMicaOrAcrylic(this);
            _config = new ConfigManager();

            string settingsGamePath = SettingsManager.Settings.GamePath;
            string effectiveMcPath;
            if (!string.IsNullOrWhiteSpace(settingsGamePath))
            {
                effectiveMcPath = Environment.ExpandEnvironmentVariables(settingsGamePath);
            }
            else
            {
                effectiveMcPath = _config.GetMinecraftPath();
            }

            AppContext.Initialize(username, isOfflineMode, minecraftPath: effectiveMcPath, config: _config);
            _launcherCore = new LauncherCore(_config);
            App.InitializeBedrockServices(effectiveMcPath);
            AppContext.NavigateRequested += pageKey => Dispatcher.Invoke(() => ShowPage(pageKey));
            ProfilePage.OnAvatarChanged += () => Dispatcher.Invoke(() => UpdateCurrentUserInfo(AppContext.Username, AppContext.IsOfflineMode));
            Loaded += (s, e) =>
            {
                InitializeBackground();
                UpdateCurrentUserInfo(username, isOfflineMode);
                ShowPage("Home");
                AnimateSidebarEntry();
            };
        }

        private void AnimateSidebarEntry()
        {
            // 侧边栏从左侧滑入
            SidebarBorder.RenderTransform = new TranslateTransform(-60, 0);
            SidebarBorder.Opacity = 0;

            var sb = new Storyboard();

            var slideIn = new DoubleAnimation(-60, 0, TimeSpan.FromSeconds(0.4))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideIn, SidebarBorder);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            sb.Children.Add(slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fadeIn, SidebarBorder);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeIn);

            sb.Completed += (_, _) =>
            {
                SidebarBorder.ClearValue(UIElement.RenderTransformProperty);
                SidebarBorder.ClearValue(UIElement.OpacityProperty);
            };

            sb.Begin();
        }

        /// <summary>
        /// 更新主窗口左下角的用户名、模式、以及用户自定义头像。
        /// 优先从 avatars/{username}.png 加载用户上传的自定义图片；不存在则使用简洁默认图。
        /// </summary>
        private void UpdateCurrentUserInfo(string username, bool isOfflineMode)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                    txtCurrentUserName.Text = username;
                txtCurrentUserMode.Text = isOfflineMode ? "离线模式" : "正版模式";
                imgCurrentUserHead.Source = BuildAvatarForUser(username);
            }
            catch
            {
                // 兜底：生成一个简洁默认头像图片
                imgCurrentUserHead.Source = BuildDefaultAvatar();
            }
        }

        /// <summary>为指定玩家构建用户头像（非 MC 皮肤头部）。</summary>
        public static BitmapSource BuildAvatarForUser(string username)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string avatarFile = Path.Combine(exeDir, "avatars", $"{username}.png");
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
            return BuildDefaultAvatar();
        }

        /// <summary>生成简洁默认头像（128×128，紫蓝渐变圆 + 白色人物字符）。</summary>
        public static BitmapImage BuildDefaultAvatar()
        {
            const int size = 128;
            var visual = new DrawingVisual();
            
            using (var dc = visual.RenderOpen())
            {
                var center = new Point(size / 2.0, size / 2.0);
                var radius = size / 2.0 - 1;

                var gradient = new RadialGradientBrush
                {
                    Center = center,
                    GradientOrigin = center,
                    RadiusX = radius,
                    RadiusY = radius,
                    MappingMode = BrushMappingMode.Absolute
                };
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xA2, 0x9B, 0xFE), 0));
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x7B, 0x68, 0xE1), 0.7));
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x5B, 0x48, 0xD3), 1));

                dc.DrawEllipse(gradient, null, center, radius, radius);

                var text = new FormattedText(
                    "👤",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"),
                    64,
                    Brushes.White
                );
                
                double textX = size / 2.0 - text.WidthIncludingTrailingWhitespace / 2.0;
                double textY = size / 2.0 - text.Height / 2.0;
                
                dc.DrawText(text, new Point(textX, textY));
            }

            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);

            var result = new BitmapImage();
            using (var ms = new System.IO.MemoryStream())
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(renderTarget));
                enc.Save(ms);
                ms.Position = 0;
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = ms;
                result.EndInit();
                result.Freeze();
            }
            return result;
        }

        /// <summary>为指定玩家构建头部正面立雕。</summary>
        public static BitmapSource BuildHeadForUser(string username)
        {
            // 先尝试 skins/{username}.png；再尝试 skins/{uuid}.png
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string skinFile = Path.Combine(exeDir, "skins", $"{username}.png");
            if (!File.Exists(skinFile))
            {
                // 离线 UUID：MD5("OfflinePlayer:" + username)
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                    string uuid = new System.Guid(bytes).ToString("N");
                    skinFile = Path.Combine(exeDir, "skins", $"{uuid}.png");
                }
            }

            if (File.Exists(skinFile))
            {
                try
                {
                    var decoder = BitmapDecoder.Create(new Uri(skinFile),
                        BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];
                    if (frame.PixelWidth >= 48 && frame.PixelHeight >= 16)
                        return BuildHeadFrontFromSkin(frame);
                    return frame;
                }
                catch { }
            }

            // 回退到默认 Steve 皮肤
            bool isSlim = false;
            try { isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{username}"); } catch { }
            return BuildDefaultHead(isSlim);
        }

        /// <summary>从完整皮肤中提取头部正面立雕（内层 + 外层叠加），放大到 64×64。</summary>
        private static BitmapSource BuildHeadFrontFromSkin(BitmapSource skin)
        {
            const int headSize = 8;
            var formatted = new FormatConvertedBitmap(skin, PixelFormats.Bgra32, null, 0);
            int w = formatted.PixelWidth;
            int stride = w * 4;
            var src = new byte[stride * formatted.PixelHeight];
            formatted.CopyPixels(src, stride, 0);

            var buf = new byte[headSize * headSize * 4];
            // 内层：x=8..15, y=8..15
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sIdx = ((8 + y) * w + (8 + x)) * 4;
                    int dIdx = (y * headSize + x) * 4;
                    Buffer.BlockCopy(src, sIdx, buf, dIdx, 4);
                }
            // 外层：x=40..47, y=8..15（非透明覆盖）
            if (w >= 48)
                for (int y = 0; y < headSize; y++)
                    for (int x = 0; x < headSize; x++)
                    {
                        int sIdx = ((8 + y) * w + (40 + x)) * 4;
                        if (src[sIdx + 3] > 0)
                        {
                            int dIdx = (y * headSize + x) * 4;
                            Buffer.BlockCopy(src, sIdx, buf, dIdx, 4);
                        }
                    }
            // 8×8 → 64×64（保持像素化）
            const int outSize = 64;
            int scale = outSize / headSize;
            var outBuf = new byte[outSize * outSize * 4];
            for (int y = 0; y < outSize; y++)
                for (int x = 0; x < outSize; x++)
                {
                    int sIdx = ((y / scale) * headSize + (x / scale)) * 4;
                    int dIdx = (y * outSize + x) * 4;
                    Buffer.BlockCopy(buf, sIdx, outBuf, dIdx, 4);
                }
            var result = BitmapSource.Create(outSize, outSize, 96, 96, PixelFormats.Bgra32, null, outBuf, outSize * 4);
            result.Freeze();
            return result;
        }

        /// <summary>生成默认 Steve/Alex 头部立雕。</summary>
        public static BitmapSource BuildDefaultHead(bool isSlim)
        {
            var skin = isSlim ? DefaultSkinGenerator.CreateAlexSkin() : DefaultSkinGenerator.CreateSteveSkin();
            return BuildHeadFrontFromSkin(skin);
        }

        private UserControl CreatePage(string key)
        {
            switch (key)
            {
                case "Home": return new HomePage();
                case "Download": return new DownloadPage();
                case "Settings": return new SettingsPage();
                case "Profile": return new ProfilePage();
                case "Mods": return new ModManagerPage();
                case "Modpacks": return new ModpacksPage();
                case "JavaConfig": return new JavaConfigPage();
                case "GameVersions": return new GameVersionsPage();
                case "MultiplayerSocial": return new MultiplayerSocialPage();
                case "FriendsList": return new FriendsListPage();
                case "Mail": return new MailPage();
                case "Community": return new CommunityPage();
                case "ComponentStore": return new ComponentStorePage();
                case "ModsPage": return new ModsPage();
                case "Shaders": return new ShadersPage();
                case "Datapacks": return new DatapacksPage();
                case "Dependencies": return new DependenciesPage();
                case "SaveManager": return new SaveManagerPage();
                case "DownloadTask": return new DownloadTaskPage();
                case "Login": return new LoginPage();
                case "RecommendDetail": return new RecommendDetailPage();
                case "LoaderSelection": return new LoaderSelectionPage();
                case "LoaderVersion": return new LoaderVersionPage();
                case "ModDetail": return new ModDetailPage();
                case "AIAssistant": return new AIAssistantPage();
                case "MusicBox": return new MusicBoxPage();
                default: return null;
            }
        }

        private ContentControl Active => _activeIsA ? pageContainerA : pageContainerB;
        private ContentControl Inactive => _activeIsA ? pageContainerB : pageContainerA;

        private void ShowPage(string pageKey)
        {
            if (_isAnimating || pageKey == _currentPageKey) return;

            try
            {
                if (!_pageCache.ContainsKey(pageKey))
                {
                    var page = CreatePage(pageKey);
                    if (page == null)
                    {
                        MessageBox.Show($"鏈煡椤甸潰: {pageKey}", "閿欒", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    _pageCache[pageKey] = page;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"椤甸潰 {pageKey} 鍔犺浇澶辫触:\n{ex}", "閿欒", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _isAnimating = true;

            // 鏂伴〉闈㈡斁鍏ラ潪娲昏穬瀹瑰櫒
            var newContainer = Inactive;
            newContainer.Content = _pageCache[pageKey];
            newContainer.RenderTransform = new TranslateTransform(30, 0);
            newContainer.Opacity = 0;
            newContainer.Visibility = Visibility.Visible;

            var oldContainer = Active;

            var sb = new Storyboard();
            const double duration = 0.3;

            // 鏃ч〉闈㈡贰鍑?            if (oldContainer.Content != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(duration))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                Storyboard.SetTarget(fadeOut, oldContainer);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fadeOut);
            }

// 新页面滑入
            var slideIn = new DoubleAnimation(30, 0, TimeSpan.FromSeconds(duration))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideIn, newContainer);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            sb.Children.Add(slideIn);

// 新页面淡入
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(duration))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fadeIn, newContainer);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeIn);

            sb.Completed += (_, _) =>
            {
                // 娓呯悊鏃у鍣?                if (oldContainer.Content != null)
                {
                    oldContainer.Content = null;
                    oldContainer.Opacity = 0;
                    oldContainer.Visibility = Visibility.Collapsed;
                    oldContainer.RenderTransform = null;
                }
                // 娓呯悊鏂板鍣ㄥ姩鐢绘畫鐣?                newContainer.ClearValue(UIElement.OpacityProperty);
                newContainer.RenderTransform = null;

                _activeIsA = !_activeIsA;
                _currentPageKey = pageKey;
                _isAnimating = false;
            };

            sb.Begin();
        }

        private void BtnNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageKey)
            {
                ShowPage(pageKey);
                UpdateNavButtonStyle(button);
            }
        }

        private void UpdateNavButtonStyle(Button activeButton)
        {
            var navButtons = new[] { btnHome, btnDownload, btnMods, btnSaves, btnMultiplayer, btnDownloadTask, btnMusicBox, btnCommunity, btnComponentStore, btnProfile, btnFriends, btnAIAssistant, btnSettings };
            
            foreach (var btn in navButtons)
            {
                if (btn == activeButton)
                {
                    btn.Foreground = new SolidColorBrush(Colors.White);
                    btn.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                }
                else
                {
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                    btn.Background = Brushes.Transparent;
                }
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            OnLogout?.Invoke();
            Close();
        }

        // ── 背景装饰层管理 ──

        private void InitializeBackground()
        {
            var bg = BackgroundConfigService.Instance;
            bg.BackgroundChanged += ApplyBackground;
            bg.ModeChanged += _ => ApplyBackground();
            StateChanged += OnWindowStateChangedForBackground;
            
            // 延迟到 ContentRendered 后执行，确保 WPF 资源树完全就绪
            ContentRendered += (s2, e2) =>
            {
                bg.RefreshPanelTransparency();
                ApplyBackground();
            };
        }

        private void ApplyBackground()
        {
            var bg = BackgroundConfigService.Instance;
            bool isImage = bg.Mode == BackgroundMode.Image;
            bool isVideo = bg.Mode == BackgroundMode.Video;

            // 快速路径：仅透明度/模糊变化（模式+路径未变），跳过文件操作
            string currentState = $"{bg.Mode}|{bg.ImagePath}|{bg.VideoPath}";
            bool stateChanged = currentState != _lastBgState;
            _lastBgState = currentState;

            // 始终同步 Opacity 和 BlurRadius（实时）
            BackgroundImage.Opacity = bg.Opacity;
            BackgroundImageBlur.Radius = bg.BlurRadius;
            BackgroundVideo.Opacity = bg.Opacity;
            BackgroundVideoBlur.Radius = bg.BlurRadius;

            if (!stateChanged)
                return; // 仅参数变化，已在上面同步完毕

            // 图片
            BackgroundImage.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
            if (isImage && !string.IsNullOrWhiteSpace(bg.ImagePath) && File.Exists(bg.ImagePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(bg.ImagePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    BackgroundImage.Source = bmp;
                }
                catch { FallbackToMicaWithNotification("图片文件损坏或格式不支持，已切回默认背景"); return; }
            }

            // 视频
            if (isVideo && !string.IsNullOrWhiteSpace(bg.VideoPath) && File.Exists(bg.VideoPath))
            {
                try
                {
                    BackgroundVideo.Visibility = Visibility.Visible;
                    BackgroundVideo.Source = new Uri(bg.VideoPath);
                    BackgroundVideo.Play();
                }
                catch { FallbackToMicaWithNotification("视频文件加载失败，已切回默认背景"); return; }
            }
            else
            {
                BackgroundVideo.Visibility = Visibility.Collapsed;
                BackgroundVideo.Stop();
                BackgroundVideo.Source = null;
            }

            // Mica 模式下完全隐藏背景控件
            if (!isImage && !isVideo)
            {
                BackgroundImage.Visibility = Visibility.Collapsed;
                BackgroundImage.Source = null;
                BackgroundVideo.Visibility = Visibility.Collapsed;
                BackgroundVideo.Stop();
                BackgroundVideo.Source = null;
            }
        }

        private void OnWindowStateChangedForBackground(object sender, EventArgs e)
        {
            if (BackgroundConfigService.Instance.Mode != BackgroundMode.Video)
                return;

            if (WindowState == WindowState.Minimized)
            {
                try { BackgroundVideo.Pause(); } catch { }
            }
            else
            {
                try
                {
                    if (BackgroundVideo.Source != null)
                        BackgroundVideo.Play();
                }
                catch { }
            }
        }

        private void BackgroundVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Volume = 0;
            BackgroundVideo.Position = TimeSpan.Zero;
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            FallbackToMicaWithNotification("视频文件解码失败，已切回默认背景");
        }

        private void FallbackToMicaWithNotification(string message)
        {
            var bg = BackgroundConfigService.Instance;
            bg.SetMode(BackgroundMode.Mica);
            try
            {
                BackgroundVideo.Stop();
                BackgroundVideo.Source = null;
            }
            catch { }
            BackgroundVideo.Visibility = Visibility.Collapsed;
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
            Dispatcher.InvokeAsync(() =>
                MessageBox.Show(message, "背景", MessageBoxButton.OK, MessageBoxImage.Information));
        }
    }
}






