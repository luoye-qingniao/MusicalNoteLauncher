using System;
using System.Collections.Generic;
using System.IO;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher
{
    public static class AppContext
    {
        public static string Username { get; set; } = "Player";
        public static bool IsOfflineMode { get; set; } = true;
        public static string MinecraftPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        public static ConfigManager Config { get; set; } = new ConfigManager();
        public static RecommendItemViewModel CurrentRecommendItem { get; set; }

        /// <summary>当前选中账号的 UUID（用于皮肤加载）</summary>
        public static string CurrentAccountUuid { get; set; }

        public static event Action<string, string, bool> AccountChanged;

        /// <summary>触发账号变更通知（供其他页面调用）</summary>
        public static void NotifyAccountChanged(string name, string uuid, bool isOnline)
        {
            AccountChanged?.Invoke(name, uuid, isOnline);
        }

        public static string SelectedGameVersion { get; set; }
        public static string SelectedGameVersionUrl { get; set; }
        public static string SelectedLoaderType { get; set; }
        public static object SelectedLoaderVersion { get; set; }

        // 已扫描到的加载器可用版本列表（供 LoaderVersionPage 直接使用，避免重复请求）
        public static List<ForgeInstaller.ForgeVersionInfo> ForgeVersions { get; set; }
        public static List<FabricVersionInfo> FabricVersions { get; set; }
        public static List<NeoForgeVersionInfo> NeoForgeVersions { get; set; }
        public static List<OptiFineVersionInfo> OptiFineVersions { get; set; }

        // 上述缓存对应的 MC 版本；若切换了 MC 版本则缓存失效，需要重新获取
        public static string CachedLoaderMcVersion { get; set; }

        public static event Action<string> NavigateRequested;

        public static void NavigateTo(string pageKey)
        {
            NavigateRequested?.Invoke(pageKey);
        }

        public static void Initialize(string username, bool isOfflineMode, string minecraftPath = null, ConfigManager config = null)
        {
            Username = username;
            IsOfflineMode = isOfflineMode;
            if (minecraftPath != null) MinecraftPath = minecraftPath;
            if (config != null) Config = config;
        }
    }
}
