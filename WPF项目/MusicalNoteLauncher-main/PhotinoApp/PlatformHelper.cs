using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MusicalNoteLauncher
{
    /// <summary>跨平台抽象：文件对话框、打开文件夹、特殊路径</summary>
    public static class PlatformHelper
    {
        // ═══════════════ 特殊目录 ═══════════════

        /// <summary>应用数据目录（跨平台替代 Environment.SpecialFolder.ApplicationData）</summary>
        public static string AppDataDir
        {
            get
            {
#if WINDOWS
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return Path.Combine(home, ".config");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return Path.Combine(home, "Library", "Application Support");
                return Path.Combine(home, ".config");
#endif
            }
        }

        /// <summary>本地应用数据目录</summary>
        public static string LocalAppDataDir
        {
            get
            {
#if WINDOWS
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
                return AppDataDir;
#endif
            }
        }

        /// <summary>用户主目录</summary>
        public static string HomeDir =>
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>下载目录</summary>
        public static string DownloadDir
        {
            get
            {
#if WINDOWS
                var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(path, "Downloads");
#else
                return Path.Combine(HomeDir, "Downloads");
#endif
            }
        }

        // ═══════════════ 文件/目录打开 ═══════════════

        /// <summary>在文件管理器中打开目录</summary>
        public static void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

#if WINDOWS
            Process.Start("explorer.exe", $"\"{path}\"");
#elif LINUX
            Process.Start("xdg-open", path);
#elif MACOS
            Process.Start("open", path);
#endif
        }

        /// <summary>用默认程序打开文件</summary>
        public static void OpenFile(string path)
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
#elif LINUX
            Process.Start("xdg-open", path);
#elif MACOS
            Process.Start("open", path);
#endif
        }

        /// <summary>在浏览器中打开 URL</summary>
        public static void OpenUrl(string url)
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
#elif LINUX
            Process.Start("xdg-open", url);
#elif MACOS
            Process.Start("open", url);
#endif
        }

        // ═══════════════ 文件对话框 ═══════════════

        /// <summary>打开文件夹选择对话框，返回 null 表示取消</summary>
        public static string? PickFolder(string? title = null)
        {
#if WINDOWS
            // Windows Forms FolderBrowserDialog（需要 System.Windows.Forms）
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = title ?? "选择文件夹",
                    ShowNewFolderButton = true,
                    UseDescriptionForTitle = true
                };
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
            }
            catch
            {
                return null;
            }
#else
            // Linux/macOS: fallback - 返回当前目录（后续可集成 Gtk# 或 Zenity）
            Logger.Warn("[PlatformHelper] 文件夹选择对话框在非 Windows 平台不可用");
            return null;
#endif
        }

        /// <summary>打开文件选择对话框</summary>
        public static string? PickFile(string? filter = null, string? title = null)
        {
#if WINDOWS
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = title ?? "选择文件",
                    Filter = filter ?? "所有文件|*.*"
                };
                return dlg.ShowDialog() == true ? dlg.FileName : null;
            }
            catch
            {
                return null;
            }
#else
            Logger.Warn("[PlatformHelper] 文件选择对话框在非 Windows 平台不可用");
            return null;
#endif
        }

        // ═══════════════ Minecraft 路径 ═══════════════

        /// <summary>获取 .minecraft 目录</summary>
        public static string GetMinecraftPath()
        {
#if WINDOWS
            return Path.Combine(AppDataDir, ".minecraft");
#elif LINUX
            return Path.Combine(HomeDir, ".minecraft");
#elif MACOS
            return Path.Combine(HomeDir, "Library", "Application Support", "minecraft");
#endif
        }

        /// <summary>Java 搜索路径</summary>
        public static string[] GetJavaSearchPaths()
        {
#if WINDOWS
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
                @"C:\Program Files\Common Files\Oracle\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Eclipse Foundation",
                @"C:\Program Files\Microsoft",
            };
#elif LINUX
            return new[]
            {
                "/usr/lib/jvm",
                "/usr/java",
                "/usr/local/java",
                "/opt/jdk",
                Path.Combine(HomeDir, ".sdkman", "candidates", "java"),
            };
#elif MACOS
            return new[]
            {
                "/Library/Java/JavaVirtualMachines",
                "/usr/local/opt/openjdk/libexec/openjdk.jdk",
                Path.Combine(HomeDir, "Library/Java/JavaVirtualMachines"),
                Path.Combine(HomeDir, ".sdkman", "candidates", "java"),
            };
#endif
        }
    }
}
