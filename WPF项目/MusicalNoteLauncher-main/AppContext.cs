using System;
using System.Collections.Generic;
using System.IO;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;
using PCL.Account;

namespace MusicalNoteLauncher
{
    public static class AppContext
    {
        public static string Username { get; set; } = "Player";
        public static bool IsOfflineMode { get; set; } = true;

        private static string _minecraftPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        /// <summary>当前游戏目录路径。设置时若路径变化会触发 GameFolderChanged 事件。</summary>
        public static string MinecraftPath
        {
            get => _minecraftPath;
            set
            {
                string newPath = value ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

                if (!string.Equals(_minecraftPath?.TrimEnd('\\'), newPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    string oldPath = _minecraftPath;
                    _minecraftPath = newPath;
                    Logger.Info($"[AppContext] 游戏目录变更: {oldPath} → {newPath}");
                    GameFolderChanged?.Invoke(oldPath, newPath);
                }
            }
        }

        /// <summary>游戏目录变更事件（oldPath, newPath）</summary>
        public static event Action<string, string> GameFolderChanged;
        public static ConfigManager Config { get; set; } = new ConfigManager();
        public static RecommendItemViewModel CurrentRecommendItem { get; set; }

        /// <summary>当前选中账号的 UUID（用于皮肤加载）</summary>
        public static string CurrentAccountUuid { get; set; }

        /// <summary>青鸟账号唯一数字ID（持久化，首次生成后固定不变）</summary>
        public static string QingniaoId { get; private set; }

        /// <summary>青鸟账号显示名称（可修改）</summary>
        public static string QingniaoName { get; private set; }

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

            // 青鸟ID：从持久化存储读取，不存在则生成并保存
            QingniaoId = Settings.Get<string>("QingniaoId");
            if (string.IsNullOrEmpty(QingniaoId))
            {
                QingniaoId = GenerateQingniaoId(username);
                Settings.Set("QingniaoId", QingniaoId);
            }

            // 青鸟显示名称：优先使用已保存的名字，否则用游戏用户名
            QingniaoName = Settings.Get<string>("QingniaoName");
            if (string.IsNullOrEmpty(QingniaoName))
            {
                QingniaoName = username;
                Settings.Set("QingniaoName", QingniaoName);
            }

            if (minecraftPath != null) MinecraftPath = minecraftPath;
            if (config != null) Config = config;
        }

        /// <summary>修改青鸟账号显示名称并持久化</summary>
        public static void SetQingniaoName(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                QingniaoName = newName.Trim();
                Settings.Set("QingniaoName", QingniaoName);
            }
        }

        /// <summary>根据用户名确定性生成10位数字青鸟ID（仅首次创建时使用）</summary>
        private static string GenerateQingniaoId(string seed)
        {
            if (string.IsNullOrEmpty(seed)) seed = "Player";
            long hash = 0;
            foreach (char c in seed)
                hash = (hash * 31 + c) & 0x7FFFFFFFFFFFFFFFL;
            return (Math.Abs(hash) % 9000000000L + 1000000000L).ToString();
        }
    }
}
