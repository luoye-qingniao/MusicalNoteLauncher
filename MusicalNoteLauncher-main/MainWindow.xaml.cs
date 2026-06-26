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

        public MainWindow() : this("Player", true) { }

        public MainWindow(string username, bool isOfflineMode)
        {
            InitializeComponent();
            _config = new ConfigManager();
            string effectiveMcPath = _config.GetMinecraftPath();
            AppContext.Initialize(username, isOfflineMode, minecraftPath: effectiveMcPath, config: _config);
            _launcherCore = new LauncherCore(_config);
            App.InitializeBedrockServices(effectiveMcPath);
            AppContext.NavigateRequested += pageKey => Dispatcher.Invoke(() => ShowPage(pageKey));
            Loaded += (s, e) =>
            {
                UpdateCurrentUserInfo(username, isOfflineMode);
                ShowPage("Home");
            };
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

        /// <summary>生成简洁默认头像（128×128，紫蓝渐变圆 + 白色音符 "♪"）。</summary>
        public static BitmapImage BuildDefaultAvatar()
        {
            const int size = 128;
            var pixels = new byte[size * size * 4];
            double cx = size / 2.0, cy = size / 2.0;
            double r = size / 2.0 - 1;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int idx = (y * size + x) * 4;
                    double dx = x - cx + 0.5, dy = y - cy + 0.5;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        pixels[idx + 0] = 0; pixels[idx + 1] = 0;
                        pixels[idx + 2] = 0; pixels[idx + 3] = 0;
                        continue;
                    }

                    double t = dist / r;
                    byte r1 = 0x6C, g1 = 0x5C, b1 = 0xE7;
                    byte r2 = 0xA2, g2 = 0x9B, b2 = 0xFE;
                    pixels[idx + 2] = (byte)(r2 + (r1 - r2) * t);
                    pixels[idx + 1] = (byte)(g2 + (g1 - g2) * t);
                    pixels[idx + 0] = (byte)(b2 + (b1 - b2) * t);
                    pixels[idx + 3] = 255;
                }

            void FillRect(int x0, int y0, int w, int h, byte br, byte bg, byte bb)
            {
                for (int yy = y0; yy < y0 + h; yy++)
                    for (int xx = x0; xx < x0 + w; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                        double dxx = xx - cx + 0.5, dyy = yy - cy + 0.5;
                        if (Math.Sqrt(dxx * dxx + dyy * dyy) > r) continue;
                        int ii = (yy * size + xx) * 4;
                        pixels[ii + 0] = bb; pixels[ii + 1] = bg;
                        pixels[ii + 2] = br; pixels[ii + 3] = 255;
                    }
            }

            int stemX = (int)cx - 3, stemY = 35;
            FillRect(stemX, stemY, 6, 55, 255, 255, 255);
            FillRect(stemX - 15, stemY + 45, 25, 14, 255, 255, 255);
            FillRect(stemX - 15, stemY + 10, 25, 12, 220, 220, 255);

            var wb = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            var result = new BitmapImage();
            using (var ms = new System.IO.MemoryStream())
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(wb));
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
            const double duration = 0.2;

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
            var navButtons = new[] { btnHome, btnDownload, btnMods, btnSaves, btnMultiplayer, btnDownloadTask, btnCommunity, btnComponentStore, btnProfile, btnFriends, btnSettings };
            
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
    }
}






