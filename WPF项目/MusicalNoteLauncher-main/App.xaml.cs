using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading.Tasks;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Windows;

namespace MusicalNoteLauncher
{
    public partial class App : Application
    {
        /// <summary>全局基岩版增强下载服务实例（单例，按需初始化）</summary>
        public static BedrockEnhancedDownloadService BedrockDownloadService { get; private set; }
        /// <summary>全局基岩版离线启动器实例（单例，按需初始化）</summary>
        public static BedrockOfflineLauncher BedrockOfflineLauncher { get; private set; }

        /// <summary>WPF 侧是否已弹出更新面板（避免 WebView 侧重复弹窗）</summary>
        public static bool UpdateDialogShown { get; set; }

        /// <summary>测试模式：是否模拟普通更新检测</summary>
        private static bool _testUpdateMode;
        /// <summary>测试模式：是否模拟强制更新检测</summary>
        private static bool _testForcedUpdateMode;

        /// <summary>初始化基岩版服务（在MainWindow初始化后调用）</summary>
        public static void InitializeBedrockServices(string minecraftPath)
        {
            if (BedrockDownloadService == null)
            {
                BedrockDownloadService = new BedrockEnhancedDownloadService(minecraftPath);
                Logger.Info("基岩版下载服务已初始化");
            }
            if (BedrockOfflineLauncher == null)
            {
                BedrockOfflineLauncher = new BedrockOfflineLauncher(minecraftPath);
                Logger.Info("基岩版离线启动器已初始化");
            }
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // —— 内存优化子进程模式（以管理员权限运行） ——
            if (e.Args.Contains("--memory-optimize", StringComparer.OrdinalIgnoreCase))
            {
                RunMemoryOptimizeSubprocess();
                return;
            }

            // —— 测试模式：模拟更新检测 ——
            _testUpdateMode = e.Args.Contains("--test-update", StringComparer.OrdinalIgnoreCase);
            _testForcedUpdateMode = e.Args.Contains("--test-forced-update", StringComparer.OrdinalIgnoreCase);

            try
            {
                Logger.Info("========================================");
                Logger.Info($"应用程序启动: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Logger.Info($"操作系统: {Environment.OSVersion}");
                Logger.Info($"CLR版本: {Environment.Version}");
                Logger.Info($"应用路径: {AppDomain.CurrentDomain.BaseDirectory}");

                // 注册全局异常捕获
                RegisterGlobalExceptionHandlers();

                // 硬件加速兼容性修复
                ConfigureRenderingSettings();

                Logger.Info("渲染设置配置完成");

                // ★ MNL 环境初始化（创建目录结构、迁移配置、依赖完整性检查）
                bool depsComplete = MNLEnvironment.Initialize();
                if (!depsComplete)
                {
                    string missingList = string.Join("\n  - ", MNLEnvironment.MissingDependencies);
                    Logger.Warning($"[启动] 检测到缺失依赖:\n  - {missingList}");
                    // 程序文件缺失属于严重问题，提示用户；目录缺失已自动创建故只需警告
                    var programFileMissing = MNLEnvironment.MissingDependencies
                        .Where(d => d.StartsWith("程序文件缺失")).ToList();
                    if (programFileMissing.Count > 0)
                    {
                        string msg = $"检测到程序文件不完整，启动器可能无法正常运行:\n\n  - {string.Join("\n  - ", programFileMissing)}\n\n建议重新下载完整程序包。";
                        ModernMessageBox.ShowWarning(msg, "依赖完整性警告");
                    }
                }

                // ★ 订阅游戏目录变更事件：切换文件夹时自动清缓存并重新扫描
                AppContext.GameFolderChanged += (oldPath, newPath) =>
                {
                    Logger.Info($"[App] 游戏目录变更，触发版本重新扫描: {newPath}");
                    VersionScanService.Instance.ClearCache();
                    _ = VersionScanService.Instance.ScanAsync("游戏目录切换");
                };

                // 加载上次保存的配色主题
                ThemeColorService.LoadSavedTheme();
                Logger.Info("配色主题加载完成");

                // ★ 显式触发背景配置服务初始化（首次访问 Lazy 单例即加载配置并校验）
                _ = BackgroundConfigService.Instance;
                Logger.Info("背景配置服务初始化完成");

                // ★ 检查启动器更新
                if (!_testUpdateMode && !_testForcedUpdateMode)
                {
                    // 正常模式：异步检查服务器更新
                    _ = Task.Run(async () =>
                    {
                        await CheckAndNotifyUpdateAsync();
                    });
                }

                // 直接打开主窗口，默认使用离线模式登录
                MainWindow mainWindow = new MainWindow("Player", true);
                this.MainWindow = mainWindow;

                // 测试模式：主窗口渲染完成后弹出模拟更新面板
                if (_testUpdateMode || _testForcedUpdateMode)
                {
                    Logger.Info("[测试] 测试更新模式已激活，将在窗口显示后弹出面板");
                }

                // 用户协议弹窗由 WebView2 前端统一处理，不再使用旧 WPF 弹窗
                // TermsOfServiceDialog 已废弃，保留代码仅供参考

                // 立即显示窗口，避免白屏
                mainWindow.Show();

                // 测试模式：延迟弹出更新面板
                if (_testUpdateMode || _testForcedUpdateMode)
                {
                    Logger.Info("[测试] 测试更新模式已激活，将在窗口显示后弹出面板");

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1500);
                        mainWindow.Dispatcher.Invoke(() =>
                        {
                            Logger.Info("[测试] 正在弹出测试更新面板...");
                            try
                            {
                                ShowTestUpdatePanel();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"[测试] 弹出更新面板失败: {ex}", ex);
                                MessageBox.Show(
                                    $"更新面板测试失败:\n{ex.Message}\n\n{ex.StackTrace}",
                                    "测试错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                    });
                }
                
                Logger.Info("主窗口显示成功");
                Logger.Info("应用程序启动流程完成");

                // ★ 上报启动成功日志（非阻塞）
                _ = LogReporter.ReportStartupAsync(true);
            }
            catch (Exception ex)
            {
                string errorMsg = $"窗口创建失败: {ex.Message}\n{ex.StackTrace}";
                Logger.Error(errorMsg, ex);
                ModernMessageBox.ShowError(errorMsg, "致命错误");

                // ★ 上报启动失败日志（同步等待 3 秒超时，确保尽可能发出）
                try
                {
                    LogReporter.ReportStartupAsync(false, errorMsg).Wait(TimeSpan.FromSeconds(3));
                }
                catch { }

                Application.Current.Shutdown(1);
            }
        }

        #region 全局异常处理

        private void RegisterGlobalExceptionHandlers()
        {
            // 非UI线程未处理异常
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                string errorMsg = $"[全局异常] {ex?.Message ?? "未知错误"}\n{ex?.StackTrace}";
                Logger.Error(errorMsg, ex ?? new Exception("Unknown error"));

                // ★ 上报崩溃日志
                if (ex != null)
                {
                    try
                    {
                        LogReporter.ReportCrashAsync(ex, "后台线程", args.IsTerminating).Wait(TimeSpan.FromSeconds(3));
                    }
                    catch { }
                }
                
                // 显示错误窗口，但不阻塞
                Application.Current.Dispatcher?.Invoke(() =>
                {
                    ModernMessageBox.ShowError(errorMsg, "应用程序错误");
                });

                if (args.IsTerminating)
                {
                    Logger.Error("应用程序即将终止");
                }
            };

            // UI线程未处理异常
            DispatcherUnhandledException += (sender, args) =>
            {
                string errorMsg = $"[UI线程异常] {args.Exception.Message}\n{args.Exception.StackTrace}";
                Logger.Error(errorMsg, args.Exception);

                // ★ 上报崩溃日志
                try
                {
                    LogReporter.ReportCrashAsync(args.Exception, "UI线程", false).Wait(TimeSpan.FromSeconds(3));
                }
                catch { }
                
                ModernMessageBox.ShowError(errorMsg, "UI线程错误");
                args.Handled = true; // 阻止应用程序退出
            };

            Logger.Info("全局异常处理器注册完成");
        }

        #endregion

        #region 渲染设置配置

        private void ConfigureRenderingSettings()
        {
            try
            {
                // 获取图形硬件级别（.NET 4.8 兼容方式）
                int graphicsTier = (RenderCapability.Tier >> 16);
                Logger.Info($"检测到图形硬件级别: Tier {graphicsTier}");

                // Tier >= 2 时启用硬件加速，否则使用软件渲染
                if (graphicsTier >= 2)
                {
                    Logger.Info("启用硬件加速");
                    RenderOptions.ProcessRenderMode = RenderMode.Default;
                }
                else
                {
                    Logger.Info("硬件加速不可用，使用软件渲染");
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"渲染设置配置失败，回退到软件渲染: {ex.Message}");
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }

        #endregion

        #region 内存优化子进程

        /// <summary>
        /// --memory-optimize 子进程入口：执行优化并以释放内存量作为退出码。
        /// 正数 = 释放 MB, 0 = 无变化, -1 = 失败
        /// </summary>
        private static void RunMemoryOptimizeSubprocess()
        {
            try
            {
                // 不启动任何 UI，纯静默执行
                var result = MemoryOptimizer.OptimizeSilentAsync().GetAwaiter().GetResult();
                int exitCode = result.FreedMb > 0 ? (int)Math.Min(result.FreedMb, int.MaxValue) : (result.FreedMb == 0 ? 0 : -1);
                Logger.Info($"[内存优化子进程] 完成: 释放 {result.FreedMb} MB, 修剪 {result.ProcessesTrimmed} 进程, 退出码: {exitCode}");
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                Logger.Error($"[内存优化子进程] 失败: {ex.Message}", ex);
                Environment.Exit(-1);
            }
        }

        #endregion

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info($"应用程序退出: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Logger.Info("========================================");
            base.OnExit(e);
        }

        #region 更新检查

        /// <summary>
        /// 异步检查更新并在有可用更新时弹出更新面板
        /// </summary>
        private static async Task CheckAndNotifyUpdateAsync()
        {
            try
            {
                // 检查用户是否启用了自动检查更新
                if (!SettingsManager.Settings.CheckUpdate)
                {
                    Logger.Info("[更新检查] 用户已关闭自动检查更新，跳过");
                    return;
                }

                var versionInfo = await UpdateService.FetchLatestVersionAsync();
                if (versionInfo == null)
                {
                    Logger.Info("[更新检查] 无法获取版本信息（服务器不可用）");
                    return;
                }

                if (!versionInfo.NeedUpdate)
                {
                    Logger.Info($"[更新检查] 当前版本 {UpdateService.CurrentVersion} 已是最新");
                    return;
                }

                // 有更新可用，在 UI 线程弹出更新面板
                Logger.Info($"[更新检查] 发现新版本: {versionInfo.Version} (当前: {UpdateService.CurrentVersion})");

                UpdateDialogShown = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var updatePanel = new UpdatePanel(versionInfo);
                    updatePanel.ShowDialog();
                    UpdateDialogShown = false;
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"[更新检查] 检查过程出现异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试用：构造模拟版本数据并弹出更新面板
        /// </summary>
        private static void ShowTestUpdatePanel()
        {
            Logger.Info("[测试] 模拟更新检测模式已激活");
            var mockInfo = new UpdateService.VersionInfo
            {
                Version = "2.0.0-beta-test",
                VersionCode = 99,
                DownloadUrl = "",
                FileHash = "",
                FileSize = 15 * 1024 * 1024, // 15 MB
                ReleaseNotes = "【新功能】\n- 新增音乐盒功能，支持播放本地音乐\n- 新增AI助手对话页面\n- 优化启动速度，提升30%\n- 修复部分用户登录失败的问题\n- 改善UI动画流畅度\n\n【改进】\n- 重构了版本下载模块\n- 增强了错误日志上报\n- 背景图片支持更多格式",
                IsForced = _testForcedUpdateMode,
                NeedUpdate = true,
            };
            var panel = new UpdatePanel(mockInfo);
            panel.ShowDialog();
        }

        #endregion
    }
}