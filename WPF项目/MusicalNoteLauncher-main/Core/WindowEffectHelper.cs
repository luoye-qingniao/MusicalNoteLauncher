using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MusicalNoteLauncher.Core
{
    public static class WindowEffectHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        // DWMWA constants for Windows 11
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private enum DWM_SYSTEMBACKDROP_TYPE
        {
            DWMSBT_DISABLE = 1,
            DWMSBT_MAINWINDOW = 2,   // Mica
            DWMSBT_TABBEDWINDOW = 3, // Acrylic
            DWMSBT_THUMBNAIL = 4
        }

        #endregion

        /// <summary>
        /// 当前是否成功启用了 Mica/Acrylic 效果。
        /// BackgroundConfigService 据此决定是否将 BackgroundBrush 设为透明。
        /// </summary>
        public static bool IsEffectActive { get; private set; }

        /// <summary>
        /// 尝试启用 Mica 效果（Windows 11 22000+）
        /// 如果失败则回退到 Acrylic（Windows 10 1803+）
        /// 再失败则不做任何处理。
        /// </summary>
        /// <returns>true 表示成功启用了某种背景效果；false 表示所有效果均失败，需要自行设置后备背景</returns>
        public static bool EnableMicaOrAcrylic(Window window, bool useDarkMode = true)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;

                if (hwnd == IntPtr.Zero)
                {
                    window.SourceInitialized += (s, e) =>
                    {
                        IntPtr h = new WindowInteropHelper(window).Handle;
                        bool result = TryEnableMica(h, useDarkMode);
                        IsEffectActive = result;
                        if (result)
                        {
                            EnableTransparentBackground(window);
                        }
                        else
                        {
                            SetFallbackBackground(window, useDarkMode);
                        }
                    };
                    return true; // 异步等待 SourceInitialized，先返回 true 避免主窗口过早设后备色
                }

                bool success = TryEnableMica(hwnd, useDarkMode);
                IsEffectActive = success;
                if (success)
                {
                    EnableTransparentBackground(window);
                }
                else
                {
                    SetFallbackBackground(window, useDarkMode);
                }
                return success;
            }
            catch
            {
                SetFallbackBackground(window, useDarkMode);
                return false;
            }
        }

        /// <summary>
        /// 所有背景效果均失败时，将窗口背景绑定到主题的 BackgroundBrush，
        /// 使其跟随 ThemeColorService 切换自动更新。
        /// </summary>
        private static void SetFallbackBackground(Window window, bool useDarkMode)
        {
            try
            {
                window.Dispatcher.Invoke(() =>
                {
                    window.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush");
                });
            }
            catch { }
        }

        /// <summary>
        /// Mica/Acrylic 效果启用成功后，用半透明主题色覆盖窗口背景，
        /// 既让 DWM Mica 效果穿透，又使背景色调跟随当前主题。
        /// 关键步骤：设置 HwndSource.CompositionTarget.BackgroundColor = Colors.Transparent，
        /// 否则 WPF 渲染表面会用不透明色填充客户区，遮挡 DWM 绘制的 Mica。
        /// </summary>
        private static void EnableTransparentBackground(Window window)
        {
            try
            {
                window.Dispatcher.Invoke(() =>
                {
                    ApplyThemeTintedBackground(window);

                    // 关键步骤：让 WPF 渲染表面真正透明
                    var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                    if (hwndSource != null)
                    {
                        hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
                        Logger.Info("[Mica] HwndSource.CompositionTarget.BackgroundColor 已设为透明");
                    }
                    else
                    {
                        Logger.Warning("[Mica] 无法获取 HwndSource，CompositionTarget.BackgroundColor 未设置");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Mica] EnableTransparentBackground 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据当前主题的 BackgroundColor 设置窗口背景为半透明画刷。
        /// 深色主题用高透明度让 Mica 穿透，浅色主题用低透明度保持明亮外观。
        /// </summary>
        public static void ApplyThemeTintedBackground(Window window)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                Color themeBg;
                if (resources["BackgroundColor"] is Color c)
                    themeBg = c;
                else if (resources["BackgroundBrush"] is SolidColorBrush brush)
                    themeBg = brush.Color;
                else
                    return;

                // 液态玻璃效果：深色主题 alpha 90（35%），浅色主题 alpha 120（47%），
                // 极低不透明度让 Mica/Acrylic 背景充分穿透，呈现液态玻璃质感
                double brightness = themeBg.R * 0.299 + themeBg.G * 0.587 + themeBg.B * 0.114;
                byte alpha = brightness < 128 ? (byte)90 : (byte)120;

                window.Background = new SolidColorBrush(Color.FromArgb(alpha, themeBg.R, themeBg.G, themeBg.B));
                Logger.Info($"[Mica] 主题半透明背景已应用: #{themeBg.R:X2}{themeBg.G:X2}{themeBg.B:X2}, Alpha={alpha}, Brightness={brightness:F0}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Mica] ApplyThemeTintedBackground 异常: {ex.Message}");
            }
        }

        private static bool TryEnableMica(IntPtr hwnd, bool useDarkMode)
        {
            // 关键步骤：扩展 DWM 框架到整个客户区，这是 Mica 可见的必要条件
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            int frameResult = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            Logger.Info($"[Mica] DwmExtendFrameIntoClientArea 返回: {frameResult}");

            // 先尝试 Mica（Windows 11 22H2+, build 22621+）
            if (TryEnableMicaEffect(hwnd))
            {
                SetDarkMode(hwnd, useDarkMode);
                return true;
            }

            // 尝试旧版 Mica API（Windows 11 21H2, build 22000）
            if (TryEnableMicaEffectLegacy(hwnd))
            {
                SetDarkMode(hwnd, useDarkMode);
                return true;
            }

            // Mica 失败，回退到 Acrylic（Windows 10 1803+）
            if (TryEnableAcrylic(hwnd))
            {
                return true;
            }

            // Acrylic 也失败，尝试基础模糊效果（Windows 10 较早版本）
            TryEnableBlurBehind(hwnd);
            return false;
        }

        private static bool TryEnableMicaEffect(IntPtr hwnd)
        {
            try
            {
                if (!DwmIsCompositionAvailable())
                {
                    Logger.Info("[Mica] DWM 合成不可用");
                    return false;
                }

                int backdropType = (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType, sizeof(int));

                Logger.Info($"[Mica] DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE) 返回: {result}");
                return result == 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Mica] TryEnableMicaEffect 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 旧版 Mica API（Win11 21H2, DWMWA_MICA = 1029），用于兼容较早的 Windows 11 版本。
        /// </summary>
        private static bool TryEnableMicaEffectLegacy(IntPtr hwnd)
        {
            try
            {
                if (!DwmIsCompositionAvailable())
                {
                    Logger.Info("[Mica] 旧版 API DWM 合成不可用");
                    return false;
                }

                int micaEnabled = 1;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT,
                    ref micaEnabled, sizeof(int));

                Logger.Info($"[Mica] 旧版 API DwmSetWindowAttribute(DWMWA_MICA_EFFECT) 返回: {result}");
                return result == 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Mica] TryEnableMicaEffectLegacy 异常: {ex.Message}");
                return false;
            }
        }

        private static void SetDarkMode(IntPtr hwnd, bool enable)
        {
            try
            {
                int useDark = enable ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDark, sizeof(int));
            }
            catch
            {
                // 静默失败
            }
        }

        private static bool TryEnableAcrylic(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = unchecked((int)0xCC1A1A2E)
                };

                int accentStructSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentStructSize
                };

                int result = SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);

                Logger.Info($"[Acrylic] SetWindowCompositionAttribute 返回: {result}");
                return result != 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Acrylic] TryEnableAcrylic 异常: {ex.Message}");
                return false;
            }
        }

        private static bool DwmIsCompositionAvailable()
        {
            try
            {
                bool enabled;
                return DwmIsCompositionEnabled(out enabled) == 0 && enabled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 最基础的后备模糊效果（ACCENT_ENABLE_BLURBEHIND），兼容较老的 Windows 10 版本。
        /// </summary>
        private static bool TryEnableBlurBehind(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                    AccentFlags = 0,
                    GradientColor = 0
                };

                int accentStructSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentStructSize
                };

                int result = SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);

                return result != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
