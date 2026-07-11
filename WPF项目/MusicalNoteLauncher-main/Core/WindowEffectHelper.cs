using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
        /// 尝试启用 Mica 效果（Windows 11 22000+）
        /// 如果失败则回退到 Acrylic（Windows 10 1803+）
        /// 再失败则不做任何处理
        /// </summary>
        public static void EnableMicaOrAcrylic(Window window, bool useDarkMode = true)
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
                        TryEnableMica(h, useDarkMode);
                    };
                    return;
                }

                TryEnableMica(hwnd, useDarkMode);
            }
            catch
            {
                // 静默失败，不影响程序运行
            }
        }

        private static void TryEnableMica(IntPtr hwnd, bool useDarkMode)
        {
            // 先尝试 Mica（Windows 11）
            if (TryEnableMicaEffect(hwnd))
            {
                // Mica 成功，同时启用深色模式
                SetDarkMode(hwnd, useDarkMode);
                return;
            }

            // Mica 失败，回退到 Acrylic（Windows 10）
            if (TryEnableAcrylic(hwnd))
            {
                return;
            }

            // Acrylic 也失败，不做处理
        }

        private static bool TryEnableMicaEffect(IntPtr hwnd)
        {
            try
            {
                // 检查 DWM 是否可用
                if (!DwmIsCompositionAvailable())
                    return false;

                // 设置 Mica 背景类型
                int backdropType = (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType, sizeof(int));

                return result == 0;
            }
            catch
            {
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
                    GradientColor = unchecked((int)0xCC1A1A2E) // 半透明深色
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

                return result == 0;
            }
            catch
            {
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
    }
}
