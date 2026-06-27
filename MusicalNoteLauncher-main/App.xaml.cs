using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading.Tasks;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher
{
    public partial class App : Application
    {
        /// <summary>全局基岩版增强下载服务实例（单例，按需初始化）</summary>
        public static BedrockEnhancedDownloadService BedrockDownloadService { get; private set; }
        /// <summary>全局基岩版离线启动器实例（单例，按需初始化）</summary>
        public static BedrockOfflineLauncher BedrockOfflineLauncher { get; private set; }

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

                // 加载上次保存的配色主题
                ThemeColorService.LoadSavedTheme();
                Logger.Info("配色主题加载完成");

                // ★ 显式触发背景配置服务初始化（首次访问 Lazy 单例即加载配置并校验）
                _ = BackgroundConfigService.Instance;
                Logger.Info("背景配置服务初始化完成");

                // ★ 检查启动器更新（非阻塞，不卡主窗口）
                _ = Task.Run(async () =>
                {
                    await CheckAndNotifyUpdateAsync();
                });

                // 直接打开主窗口，默认使用离线模式登录
                MainWindow mainWindow = new MainWindow("Player", true);
                this.MainWindow = mainWindow;
                
                // 立即显示窗口，避免白屏
                mainWindow.Show();
                
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
        /// 异步检查更新并在有可用更新时通过消息框通知用户
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

                // 有更新可用，在 UI 线程弹窗
                Logger.Info($"[更新检查] 发现新版本: {versionInfo.Version} (当前: {UpdateService.CurrentVersion})");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    string message = $"发现新版本 {versionInfo.Version}\n\n" +
                                     $"更新内容:\n{versionInfo.ReleaseNotes}\n\n" +
                                     $"是否立即更新？";

                    if (versionInfo.IsForced)
                    {
                        message = $"【强制更新】发现新版本 {versionInfo.Version}\n\n" +
                                  $"更新内容:\n{versionInfo.ReleaseNotes}\n\n" +
                                  $"此版本为强制更新，启动器将自动下载安装。";
                    }

                    bool userWantsUpdate = versionInfo.IsForced;

                    if (!versionInfo.IsForced)
                    {
                        var result = ModernMessageBox.ShowConfirm(message, "版本更新");
                        userWantsUpdate = result;
                    }
                    else
                    {
                        ModernMessageBox.ShowInfo(message, "版本更新（强制）");
                    }

                    if (userWantsUpdate)
                    {
                        _ = Task.Run(async () =>
                        {
                            bool success = await UpdateService.DownloadAndInstallAsync(
                                versionInfo,
                                progress => Logger.Info($"[更新] {progress.Status} ({progress.ProgressPercent}%)"));

                            if (success)
                            {
                                // 安装脚本已启动，退出当前进程
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Application.Current.Shutdown(0);
                                });
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ModernMessageBox.ShowError("更新安装失败，请稍后重试", "更新失败");
                                });
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"[更新检查] 检查过程出现异常: {ex.Message}");
            }
        }

        #endregion
    }
}