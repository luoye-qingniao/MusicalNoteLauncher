using System.Diagnostics;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Backend;

/// <summary>
/// 跨平台文件/URL 操作辅助类。
/// </summary>
public static class PlatformHelper
{
    public static void OpenUrl(string url)
    {
        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
#elif LINUX
            Process.Start("xdg-open", url);
#elif MACOS
            Process.Start("open", url);
#else
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
#endif
        }
        catch (Exception ex)
        {
            Logger.Error($"[PlatformHelper] OpenUrl 失败: {ex.Message}");
        }
    }

    public static void OpenFolder(string path)
    {
        try
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
        catch (Exception ex)
        {
            Logger.Error($"[PlatformHelper] OpenFolder 失败: {ex.Message}");
        }
    }

    public static void OpenFile(string path)
    {
        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
#elif LINUX
            Process.Start("xdg-open", path);
#elif MACOS
            Process.Start("open", path);
#endif
        }
        catch (Exception ex)
        {
            Logger.Error($"[PlatformHelper] OpenFile 失败: {ex.Message}");
        }
    }
}
