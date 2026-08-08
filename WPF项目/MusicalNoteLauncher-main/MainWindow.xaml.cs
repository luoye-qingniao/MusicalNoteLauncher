using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MusicalNoteLauncher.Pages;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly LauncherCore _launcherCore;
        private WebBridge _bridge;
        private HttpListener? _httpServer;
        private int _httpPort;
        public event Action OnLogout;
        public event Action OnMsLogin;
        public static event Action RefreshSidebarRequested;
        public static void RequestSidebarRefresh() => RefreshSidebarRequested?.Invoke();

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
            AppContext.NavigateRequested += pageKey => Dispatcher.Invoke(() => NavigateToPage(pageKey));
            RefreshSidebarRequested += () => Dispatcher.Invoke(() => UpdateSidebarUserInfo(username, isOfflineMode));
            ProfilePage.OnAvatarChanged += () => Dispatcher.Invoke(() => UpdateSidebarUserInfo(username, isOfflineMode));

            // 微软正版登录（从 WebView2 前端触发）
            OnMsLogin += () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    await RunMicrosoftLoginAsync();
                });
            };

            Loaded += async (s, e) =>
            {
                InitializeBackground();
                await InitializeWebViewAsync();
                // 将音乐播放服务绑定到全局 MediaElement
                MusicPlayerService.Instance.BindPlayer(GlobalMediaPlayer);
            };
            Closed += (s, e) =>
            {
                _httpServer?.Stop();
                _httpServer?.Close();
            };
        }

        /// <summary>获取 LauncherCore 实例（供 WebBridge 使用）</summary>
        public LauncherCore GetLauncherCore() => _launcherCore;

        /// <summary>触发退出登录事件（供 WebBridge 使用）</summary>
        public void TriggerLogout() => OnLogout?.Invoke();

        /// <summary>触发微软登录事件（供 WebBridge 使用）</summary>
        public void TriggerMsLogin() => OnMsLogin?.Invoke();

        /// <summary>执行 Microsoft Device Code Flow 登录（自动轮询，无需用户粘贴 URL）</summary>
        private async System.Threading.Tasks.Task RunMicrosoftLoginAsync()
        {
            bool completed = false;
            var cts = new System.Threading.CancellationTokenSource();

            Action<string, string> deviceCodeHandler = null;
            deviceCodeHandler = (userCode, verificationUri) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 打开浏览器到 microsoft.com/link
                    try
                    {
                        Log($"[MsAuth] 设备码: {userCode}, 打开浏览器: {verificationUri}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(verificationUri) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Log($"[MsAuth] 浏览器打开失败: {ex.Message}");
                    }

                    _bridge?.PostMessage("ms-device-code", new
                    {
                        userCode = userCode,
                        verificationUri = verificationUri,
                        message = "请在浏览器中输入验证码完成登录"
                    });
                }));
            };

            // 监听前端取消事件
            Action cancelHandler = null;
            cancelHandler = () =>
            {
                Log("[MsAuth] 收到前端取消请求，停止轮询");
                cts.Cancel();
            };
            _bridge.MsAuthCancelHandler = cancelHandler;

            try
            {
                Log("[MsAuth] 开始 Device Code 登录流程...");
                PCL.Auth.Microsoft.MinecraftMsAuth.OnDeviceCodeRequired += deviceCodeHandler;

                var authTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    Log("[MsAuth] LoginWithDeviceCodeAsync 启动...");
                    return await PCL.Auth.Microsoft.MinecraftMsAuth.LoginWithDeviceCodeAsync(
                        null, cts.Token);
                });

                var result = await authTask;
                completed = true;
                Log($"[MsAuth] 登录成功: {result.UserName}");

                // 保存登录结果（双重持久化：PCL accounts.json + MNL game_accounts.json）
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
                WebBridge.SaveMicrosoftAccount(result.UserName);

                // 更新侧边栏信息
                UpdateSidebarUserInfo(result.UserName, false);

                // 通知 WebApp 前端
                _bridge?.PostMessage("ms-login-success", new
                {
                    userName = result.UserName,
                    uuid = result.Uuid,
                    accessToken = result.McAccessToken,
                    type = "microsoft"
                });

                _bridge?.PostMessage("toast", new
                {
                    message = "微软账号 " + result.UserName + " 登录成功！",
                    type = "success"
                });
            }
            catch (PCL.Auth.Microsoft.MsAuthException ex)
            {
                Log($"[MsAuth] 登录失败 (MsAuthException): {ex.Message}");
                if (!completed)
                {
                    _bridge?.PostMessage("ms-login-error", new { error = ex.Message });
                }
                _bridge?.PostMessage("toast", new { message = "登录失败: " + ex.Message, type = "error" });
            }
            catch (OperationCanceledException)
            {
                Log("[MsAuth] 用户取消登录");
                // 不弹错误 toast，弹一个温和的提示
                _bridge?.PostMessage("toast", new { message = "已取消登录", type = "info" });
            }
            catch (Exception ex)
            {
                Log($"[MsAuth] 登录失败 (Exception): {ex}");
                if (!completed)
                {
                    _bridge?.PostMessage("ms-login-error", new { error = ex.Message });
                }
                _bridge?.PostMessage("toast", new { message = "登录失败: " + ex.Message, type = "error" });
            }
            finally
            {
                Log("[MsAuth] 清理事件订阅");
                PCL.Auth.Microsoft.MinecraftMsAuth.OnDeviceCodeRequired -= deviceCodeHandler;
                _bridge.MsAuthCancelHandler = null;
                cts.Dispose();
            }
        }

        // ── WebView2 初始化 ──

        private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mnl_webview.log");
        private static readonly object _logLock = new();

        private static void Log(string message)
        {
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(_logPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch { /* 日志写入失败不应阻塞主流程 */ }
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                // 预检：WebView2 Runtime 是否已安装
                string? runtimeVersion = GetWebView2RuntimeVersion();
                if (string.IsNullOrEmpty(runtimeVersion))
                {
                    var result = MessageBox.Show(
                        "未检测到 WebView2 Runtime，MNL 启动器需要它来显示界面。\n\n" +
                        "点击 [确定] 将打开下载页面（约 120MB，安装后重启即可）。",
                        "缺少 WebView2 Runtime",
                        MessageBoxButton.OKCancel, MessageBoxImage.Information);

                    if (result == MessageBoxResult.OK)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                            UseShellExecute = true
                        });
                    }
                    Application.Current.Shutdown();
                    return;
                }

                // 预检通过，用 CoreWebView2Environment 指定用户数据目录 + WebGL 标志
                Log($"[WebView2] Runtime 已安装: {runtimeVersion}");
                Log("[WebView2] 开始初始化...");

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MNL", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                // 启用 WebGL 支持（3D 皮肤预览需要）+ 允许无用户手势自动播放视频壁纸
                var envOptions = new CoreWebView2EnvironmentOptions(
                    "--enable-webgl --ignore-gpu-blocklist --autoplay-policy=no-user-gesture-required");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, envOptions);
                await WebView.EnsureCoreWebView2Async(env);

                // 注册 C# 桥接对象
                _bridge = new WebBridge(this);
                WebView.CoreWebView2.AddHostObjectToScript("mnl", _bridge);

                // 配置 WebView2 设置
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

                // 虚拟主机映射: 视频背景通过独立域名加载，绕过 WebView2 对 localhost 的媒体限制
                var bgFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "backgrounds");
                Directory.CreateDirectory(bgFolder);
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "mnl-bg.local", bgFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                // 启动本地 HTTP 服务器并在 WebView2 中加载
                string webAppFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebApp");
                if (Directory.Exists(webAppFolder) && File.Exists(Path.Combine(webAppFolder, "index.html")))
                {
                    StartHttpServer(webAppFolder);
                    Log($"[WebView2] HTTP 服务器: http://localhost:{_httpPort}/");
                    // 逐步加载测试
                    WebView.CoreWebView2.Navigate($"http://localhost:{_httpPort}/index.html");
                }
                else
                {
                    WebView.CoreWebView2.NavigateToString(
                        "<html><body style='display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif'>" +
                        "<h2>WebApp 文件未找到</h2></body></html>");
                }
            }
            catch (Exception ex)
            {
                Log($"[WebView2] 异常: {ex}");
                MessageBox.Show($"WebView2 初始化失败: {ex.Message}\n\n" +
                    "请安装 WebView2 Runtime 后重试。",
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        /// <summary>检测 WebView2 Runtime 是否已安装，返回版本号或 null</summary>
        private static string? GetWebView2RuntimeVersion()
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>获取 WebView2 Runtime 安装路径</summary>
        private static string? GetWebView2BrowserFolder()
        {
            // 系统级安装路径
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "EdgeWebView", "Application"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft", "EdgeWebView", "Application"),
                // 用户级安装路径
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "EdgeWebView", "Application"),
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var dirs = Directory.GetDirectories(path);
                    if (dirs.Length > 0) return path;
                }
            }
            return null;
        }

        /// <summary>C# 端触发页面导航（通过 WebView2 发送消息到前端）</summary>
        private void NavigateToPage(string pageKey)
        {
            _bridge?.PostMessage("navigation", new { page = pageKey });
        }

        /// <summary>更新侧边栏用户信息（通过 WebView2 发送消息到前端）</summary>
        private void UpdateSidebarUserInfo(string username, bool isOfflineMode)
        {
            _bridge?.PostMessage("user-update", new
            {
                username = username ?? "Player",
                isOffline = isOfflineMode,
                mode = isOfflineMode ? "离线模式" : "正版模式"
            });
        }

        // ── 头像生成（供其他页面使用） ──

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
            using (var ms = new MemoryStream())
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

        public static BitmapSource BuildHeadForUser(string username)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string skinFile = Path.Combine(exeDir, "skins", $"{username}.png");
            if (!File.Exists(skinFile))
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                    string uuid = new Guid(bytes).ToString("N");
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

            bool isSlim = false;
            try { isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{username}"); } catch { }
            return BuildDefaultHead(isSlim);
        }

        private static BitmapSource BuildHeadFrontFromSkin(BitmapSource skin)
        {
            const int headSize = 8;
            var formatted = new FormatConvertedBitmap(skin, PixelFormats.Bgra32, null, 0);
            int w = formatted.PixelWidth;
            int stride = w * 4;
            var src = new byte[stride * formatted.PixelHeight];
            formatted.CopyPixels(src, stride, 0);

            var buf = new byte[headSize * headSize * 4];
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sIdx = ((8 + y) * w + (8 + x)) * 4;
                    int dIdx = (y * headSize + x) * 4;
                    Buffer.BlockCopy(src, sIdx, buf, dIdx, 4);
                }
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

        public static BitmapSource BuildDefaultHead(bool isSlim)
        {
            var skin = isSlim ? DefaultSkinGenerator.CreateAlexSkin() : DefaultSkinGenerator.CreateSteveSkin();
            return BuildHeadFrontFromSkin(skin);
        }

        // ── 背景装饰层管理 ──

        public void InitializeBackground()
        {
            var bg = BackgroundConfigService.Instance;
            bg.BackgroundChanged += ApplyBackground;
            bg.ModeChanged += _ => ApplyBackground();
            StateChanged += OnWindowStateChangedForBackground;

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

            string currentState = $"{bg.Mode}|{bg.ImagePath}|{bg.VideoPath}";
            bool stateChanged = currentState != _lastBgState;
            _lastBgState = currentState;

            BackgroundImage.Opacity = bg.Opacity;
            BackgroundImageBlur.Radius = bg.BlurRadius;
            BackgroundVideo.Opacity = bg.Opacity;
            BackgroundVideoBlur.Radius = bg.BlurRadius;

            if (!stateChanged) return;

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
            if (BackgroundConfigService.Instance.Mode != BackgroundMode.Video) return;
            if (WindowState == WindowState.Minimized)
            {
                try { BackgroundVideo.Pause(); } catch { }
            }
            else
            {
                try
                {
                    if (BackgroundVideo.Source != null) BackgroundVideo.Play();
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

        #region 本地 HTTP 文件服务器 (解决中文路径 file:// 乱码 / CDN 劫持问题)
        private void StartHttpServer(string rootFolder)
        {
            rootFolder = Path.GetFullPath(rootFolder);
            _httpPort = FindAvailablePort();
            _httpServer = new HttpListener();
            _httpServer.Prefixes.Add($"http://localhost:{_httpPort}/");
            _httpServer.Start();
            // 后台线程处理请求
            _ = Task.Run(() => ServeHttpRequests(rootFolder));
        }

        private static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async void ServeHttpRequests(string rootFolder)
        {
            while (_httpServer?.IsListening == true)
            {
                try
                {
                    var ctx = await _httpServer.GetContextAsync();
                    var rawPath = ctx.Request.Url!.AbsolutePath;
                    var filePath = rawPath.TrimStart('/').Split('?')[0];
                    if (string.IsNullOrEmpty(filePath) || filePath.EndsWith("/"))
                        filePath += "index.html";

                    // Debug endpoint: show first bytes of vue.js
                    if (filePath == "debug/vue-head")
                    {
                        var vuePath = Path.Combine(rootFolder, "vue.js");
                        if (File.Exists(vuePath))
                        {
                            using var fs = File.OpenRead(vuePath);
                            var headBytes = new byte[Math.Min(200, fs.Length)];
                            fs.Read(headBytes, 0, headBytes.Length);
                            var hex = BitConverter.ToString(headBytes).Replace("-", " ");
                            var text = System.Text.Encoding.UTF8.GetString(headBytes);
                            var response = $"Size: {fs.Length} bytes\n\nHex: {hex}\n\nText: {text}";
                            var respBytes = System.Text.Encoding.UTF8.GetBytes(response);
                            ctx.Response.ContentType = "text/plain; charset=utf-8";
                            ctx.Response.ContentLength64 = respBytes.Length;
                            await ctx.Response.OutputStream.WriteAsync(respBytes, 0, respBytes.Length);
                        }
                        ctx.Response.Close();
                        continue;
                    }

                    // API: 提供皮肤文件（3D 皮肤预览用）
                    if (filePath.StartsWith("api/skin/"))
                    {
                        var skinId = filePath.Substring("api/skin/".Length).Split('/')[0];
                        if (!string.IsNullOrEmpty(skinId) && !skinId.Contains(".."))
                        {
                            var skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                            var skinFile = Path.Combine(skinsDir, skinId + ".png");
                            if (File.Exists(skinFile))
                            {
                                ctx.Response.ContentType = "image/png";
                                ctx.Response.AddHeader("Cache-Control", "no-cache");
                                ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                                using var fs = File.OpenRead(skinFile);
                                ctx.Response.ContentLength64 = fs.Length;
                                await fs.CopyToAsync(ctx.Response.OutputStream);
                            }
                            else
                            {
                                ctx.Response.StatusCode = 404;
                            }
                        }
                        else
                        {
                            ctx.Response.StatusCode = 400;
                        }
                        ctx.Response.Close();
                        continue;
                    }

                    // API: 提供背景图片/视频文件
                    if (filePath.StartsWith("api/bg/"))
                    {
                        try
                        {
                            var bgFileName = filePath.Substring("api/bg/".Length).Split('/')[0];
                            if (!string.IsNullOrEmpty(bgFileName) && !bgFileName.Contains(".."))
                            {
                                var bgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "backgrounds");
                                var bgFile = Path.Combine(bgDir, bgFileName);
                                if (File.Exists(bgFile))
                                {
                                    var bgExt = Path.GetExtension(bgFile).ToLowerInvariant();
                                    ctx.Response.ContentType = bgExt switch { ".jpg" => "image/jpeg", ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".mp4" => "video/mp4", _ => "application/octet-stream" };
                                    ctx.Response.AddHeader("Cache-Control", "no-cache");
                                    ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                                    ctx.Response.AddHeader("Accept-Ranges", "bytes");

                                    // 处理视频 Range 请求（WebView2 视频播放必需）
                                    var rangeHeader = ctx.Request.Headers["Range"];
                                    if (!string.IsNullOrEmpty(rangeHeader) && bgExt == ".mp4")
                                    {
                                        using var fs = File.OpenRead(bgFile);
                                        var totalSize = fs.Length;
                                        // 解析 Range: bytes=start-end
                                        var range = rangeHeader.Replace("bytes=", "").Split('-');
                                        long start = long.TryParse(range[0], out var s) ? s : 0;
                                        long end = (range.Length > 1 && long.TryParse(range[1], out var e)) ? e : totalSize - 1;
                                        end = Math.Min(end, totalSize - 1);
                                        var contentLength = end - start + 1;

                                        ctx.Response.StatusCode = 206;
                                        ctx.Response.AddHeader("Content-Range", $"bytes {start}-{end}/{totalSize}");
                                        ctx.Response.ContentLength64 = contentLength;
                                        await ctx.Response.OutputStream.FlushAsync();

                                        fs.Seek(start, SeekOrigin.Begin);
                                        var buffer = new byte[81920];
                                        long remaining = contentLength;
                                        while (remaining > 0)
                                        {
                                            var toRead = (int)Math.Min(buffer.Length, remaining);
                                            var read = await fs.ReadAsync(buffer, 0, toRead);
                                            if (read == 0) break;
                                            await ctx.Response.OutputStream.WriteAsync(buffer, 0, read);
                                            remaining -= read;
                                        }
                                    }
                                    else
                                    {
                                        ctx.Response.ContentLength64 = new FileInfo(bgFile).Length;
                                        await ctx.Response.OutputStream.FlushAsync();
                                        using var fs = File.OpenRead(bgFile);
                                        await fs.CopyToAsync(ctx.Response.OutputStream);
                                        await ctx.Response.OutputStream.FlushAsync();
                                    }
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 404;
                                }
                            }
                            else
                            {
                                ctx.Response.StatusCode = 400;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"[api/bg] 处理异常: {ex.Message}");
                            try { ctx.Response.StatusCode = 500; } catch { }
                        }
                        finally
                        {
                            try { ctx.Response.Close(); } catch { }
                        }
                        continue;
                    }

                    // API: 音频流代理（让 audio 元素跨域可用 + captureStream）
                    // URL: /api/audio?url=<encoded>
                    if (filePath == "api/audio" || filePath.StartsWith("api/audio?"))
                    {
                        await HandleAudioProxyAsync(ctx);
                        continue;
                    }

                    // API: 封面图片代理（解决 Canvas getImageData 跨域问题）
                    // URL: /api/img?url=<encoded>
                    if (filePath == "api/img" || filePath.StartsWith("api/img?"))
                    {
                        await HandleImageProxyAsync(ctx);
                        continue;
                    }

                    var fullPath = Path.GetFullPath(Path.Combine(rootFolder, filePath));

                    // favicon: 用 logo.png 替代
                    if (filePath == "favicon.ico")
                    {
                        var logoPath = Path.Combine(rootFolder, "img", "logo.png");
                        if (File.Exists(logoPath))
                        {
                            ctx.Response.ContentType = "image/png";
                            ctx.Response.AddHeader("Cache-Control", "public, max-age=86400");
                            using (var fs = File.OpenRead(logoPath))
                            {
                                ctx.Response.ContentLength64 = fs.Length;
                                await fs.CopyToAsync(ctx.Response.OutputStream);
                            }
                        }
                        else
                        {
                            ctx.Response.StatusCode = 404;
                        }
                        ctx.Response.Close();
                        continue;
                    }
                    // 路径穿越防护
                    if (!fullPath.StartsWith(rootFolder + Path.DirectorySeparatorChar))
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.Close();
                        continue;
                    }

                    if (File.Exists(fullPath))
                    {
                        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                        // JS/CSS 文件直接用文件流传输，避免 ReadAllBytes 和编码问题
                        ctx.Response.ContentType = GetContentType(fullPath);
                        ctx.Response.AddHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                        using (var fs = File.OpenRead(fullPath))
                        {
                            ctx.Response.ContentLength64 = fs.Length;
                            await fs.CopyToAsync(ctx.Response.OutputStream);
                        }
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                    ctx.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    try { Logger.Warning($"[HTTP] 请求处理异常: {ex.Message}"); } catch { }
                }
            }
        }

        private static string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream"
            };
        }

        /// <summary>封面图片代理：解决 Canvas getImageData 跨域问题</summary>
        private async Task HandleImageProxyAsync(HttpListenerContext ctx)
        {
            HttpListenerResponse response = ctx.Response;
            HttpListenerRequest request = ctx.Request;
            try
            {
                var uri = new Uri(request.Url!.AbsoluteUri);
                // 手动解析 ?url= 参数（避免 System.Web 依赖）
                var rawQuery = uri.Query.TrimStart('?');
                string? targetUrl = null;
                foreach (var part in rawQuery.Split('&'))
                {
                    var kv = part.Split(new[] { '=' }, 2);
                    if (kv.Length == 2 && kv[0] == "url")
                        targetUrl = Uri.UnescapeDataString(kv[1]);
                }
                if (string.IsNullOrEmpty(targetUrl))
                {
                    response.StatusCode = 400;
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.Close();
                    return;
                }
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                var imgResponse = await client.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead);
                response.StatusCode = (int)imgResponse.StatusCode;
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Cache-Control", "public, max-age=3600");
                if (imgResponse.Content.Headers.ContentType != null)
                    response.ContentType = imgResponse.Content.Headers.ContentType.ToString();
                using var stream = await imgResponse.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(response.OutputStream);
            }
            catch { response.StatusCode = 502; }
            finally { try { response.Close(); } catch { } }
        }

        /// <summary>音频流代理：让 audio 元素跨域加载 + captureStream 可用</summary>
        private async Task HandleAudioProxyAsync(HttpListenerContext ctx)
        {
            HttpListenerResponse response = ctx.Response;
            HttpListenerRequest request = ctx.Request;
            try
            {
                // 解析 ?url= 参数
                string targetUrl = null;
                if (request.Url != null && !string.IsNullOrEmpty(request.Url.Query))
                {
                    var qParams = request.Url.Query.TrimStart('?').Split('&');
                    foreach (var p in qParams)
                    {
                        var eq = p.IndexOf('=');
                        if (eq > 0)
                        {
                            var k = Uri.UnescapeDataString(p.Substring(0, eq));
                            var v = Uri.UnescapeDataString(p.Substring(eq + 1));
                            if (k == "url") { targetUrl = v; break; }
                        }
                    }
                }
                if (string.IsNullOrEmpty(targetUrl))
                {
                    response.StatusCode = 400;
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.Close();
                    return;
                }

                Logger.Info($"[api/audio] GET {targetUrl}");

                // 使用 HttpClient 拉取上游
                var http = SafeHttpClientFactory.CreateClient(30);

                // 手动跟随重定向（最多 5 次）
                HttpResponseMessage upstream = null;
                string currentUrl = targetUrl;
                for (int redirectCount = 0; redirectCount <= 5; redirectCount++)
                {
                    var upstreamReq = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    upstreamReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36");
                    upstreamReq.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/");
                    upstreamReq.Headers.TryAddWithoutValidation("Accept", "*/*");
                    // 透传 Range（仅首请求）
                    if (redirectCount == 0)
                    {
                        var rangeHeader = request.Headers["Range"];
                        if (!string.IsNullOrEmpty(rangeHeader))
                            upstreamReq.Headers.TryAddWithoutValidation("Range", rangeHeader);
                    }
                    // 重定向请求不带 Range
                    if (redirectCount > 0)
                    {
                        upstreamReq.Content = null;
                    }

                    upstream = await http.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);
                    Logger.Info($"[api/audio] 第 {redirectCount + 1} 次响应 {(int)upstream.StatusCode} {upstream.Content.Headers.ContentType}");

                    if (((int)upstream.StatusCode == 301 || (int)upstream.StatusCode == 302 ||
                         (int)upstream.StatusCode == 303 || (int)upstream.StatusCode == 307 ||
                         (int)upstream.StatusCode == 308) && upstream.Headers.Location != null)
                    {
                        var next = upstream.Headers.Location.IsAbsoluteUri
                            ? upstream.Headers.Location.ToString()
                            : new Uri(new Uri(currentUrl), upstream.Headers.Location).ToString();
                        Logger.Info($"[api/audio] 重定向 {currentUrl} -> {next}");
                        upstream.Dispose();
                        currentUrl = next;
                        continue;
                    }
                    break;
                }

                using (upstream)
                {
                    var statusCode = (int)upstream.StatusCode;
                    Logger.Info($"[api/audio] 上游响应 {statusCode} {upstream.Content.Headers.ContentType}");

                    // 必须先设置 CORS 头，再设置状态码
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
                    response.AddHeader("Access-Control-Allow-Headers", "Range, If-Range, Content-Type");
                    response.AddHeader("Access-Control-Expose-Headers", "Content-Length, Content-Range, Accept-Ranges, Content-Type");
                    response.AddHeader("Accept-Ranges", "bytes");
                    response.AddHeader("Cache-Control", "no-cache");

                    // 透传上游头
                    if (upstream.Content.Headers.ContentRange != null)
                        response.AddHeader("Content-Range", upstream.Content.Headers.ContentRange.ToString());
                    if (upstream.Content.Headers.LastModified.HasValue)
                        response.AddHeader("Last-Modified", upstream.Content.Headers.LastModified.Value.ToString("R"));

                    // 处理 Content-Type
                    string ct = upstream.Content.Headers.ContentType?.ToString() ?? "";
                    if (string.IsNullOrEmpty(ct) ||
                        ct.IndexOf("octet-stream", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ct.IndexOf("binary", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var lower = targetUrl.ToLowerInvariant();
                        if (lower.Contains(".mp3")) ct = "audio/mpeg";
                        else if (lower.Contains(".m4a") || lower.Contains(".mp4")) ct = "audio/mp4";
                        else if (lower.Contains(".aac")) ct = "audio/aac";
                        else if (lower.Contains(".ogg")) ct = "audio/ogg";
                        else if (lower.Contains(".flac")) ct = "audio/flac";
                        else ct = "audio/mpeg";
                    }
                    response.ContentType = ct;

                    // 透传 Content-Length
                    if (upstream.Content.Headers.ContentLength.HasValue)
                        response.ContentLength64 = upstream.Content.Headers.ContentLength.Value;

                    response.StatusCode = statusCode;

                    // 流式复制上游响应
                    using (var upstreamStream = await upstream.Content.ReadAsStreamAsync())
                    using (var responseStream = response.OutputStream)
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await upstreamStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            try
                            {
                                await responseStream.WriteAsync(buffer, 0, read);
                            }
                            catch (HttpListenerException) { break; }
                            catch (System.IO.IOException) { break; }
                            catch (System.Net.Sockets.SocketException) { break; }
                        }
                        try { await responseStream.FlushAsync(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Logger.Warning($"[api/audio] 处理异常: {ex.GetType().Name}: {ex.Message}"); } catch { }
                if (ex.InnerException != null)
                {
                    try { Logger.Warning($"[api/audio] 内部异常: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"); } catch { }
                }
                try { response.StatusCode = 502; } catch { }
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }
        #endregion
    }
}
