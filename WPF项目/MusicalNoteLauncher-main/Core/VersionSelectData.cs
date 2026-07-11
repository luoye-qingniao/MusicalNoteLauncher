using System;
using System.Collections.Generic;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 版本选择页面的数据传递桥梁（跨页面静态数据）
    /// </summary>
    public static class VersionSelectData
    {
        /// <summary>待选择的版本列表（由外部预加载时使用）</summary>
        public static List<DownloadVersionInfo> Versions { get; set; }

        /// <summary>资源名称（模组名/整合包名等）</summary>
        public static string ResourceName { get; set; }

        /// <summary>下载目标目录</summary>
        public static string TargetDir { get; set; }

        /// <summary>来源类型（Modrinth / CurseForge），VersionSelectPage 自行加载时使用</summary>
        public static string Source { get; set; }

        /// <summary>项目 ID，VersionSelectPage 自行加载时使用</summary>
        public static string ProjectId { get; set; }

        /// <summary>用户选择版本后的回调（不同页面有不同的处理逻辑）</summary>
        public static Action<DownloadVersionInfo> OnVersionSelected { get; set; }

        /// <summary>返回时导航的目标页面路由名</summary>
        public static string BackPage { get; set; }

        /// <summary>已安装的游戏版本列表（供下载确认弹窗选择）</summary>
        public static List<string> InstalledVersions { get; set; }

        /// <summary>Minecraft 根目录</summary>
        public static string MinecraftPath { get; set; }

        /// <summary>当前启动器选中的游戏版本</summary>
        public static string CurrentGameVersion { get; set; }

        /// <summary>清空数据</summary>
        public static void Clear()
        {
            Versions = null;
            ResourceName = null;
            TargetDir = null;
            Source = null;
            ProjectId = null;
            OnVersionSelected = null;
            BackPage = null;
            InstalledVersions = null;
            MinecraftPath = null;
            CurrentGameVersion = null;
        }
    }
}
