using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

                // 直接打开主窗口，默认使用离线模式登录
                MainWindow mainWindow = new MainWindow("Player", true);
                this.MainWindow = mainWindow;
                
                // 立即显示窗口，避免白屏
                mainWindow.Show();
                
                Logger.Info("主窗口显示成功");
                Logger.Info("应用程序启动流程完成");
            }
            catch (Exception ex)
            {
                string errorMsg = $"窗口创建失败: {ex.Message}\n{ex.StackTrace}";
                Logger.Error(errorMsg, ex);
                MessageBox.Show(errorMsg, "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                // 显示错误窗口，但不阻塞
                Application.Current.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(errorMsg, "应用程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                MessageBox.Show(errorMsg, "UI线程错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info($"应用程序退出: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Logger.Info("========================================");
            base.OnExit(e);
        }
    }
}