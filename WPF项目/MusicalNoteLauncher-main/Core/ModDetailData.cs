using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 依赖模组信息（前置模组）
    /// </summary>
    public class DependencyMod
    {
        /// <summary>项目 ID（Modrinth 用 string，CurseForge 用 long 转 string）</summary>
        public string ProjectId { get; set; }
        /// <summary>模组名称</summary>
        public string Name { get; set; }
        /// <summary>模组图标 URL</summary>
        public string IconUrl { get; set; }
        /// <summary>来源平台</summary>
        public string Source { get; set; }
        /// <summary>是否为必需依赖</summary>
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// 模组详情页的数据传递桥梁（跨页面静态数据）
    /// </summary>
    public static class ModDetailData
    {
        /// <summary>模组名称</summary>
        public static string Name { get; set; }

        /// <summary>模组描述</summary>
        public static string Description { get; set; }

        /// <summary>作者名</summary>
        public static string Author { get; set; }

        /// <summary>下载量（格式化后的字符串）</summary>
        public static string Downloads { get; set; }

        /// <summary>图标 URL</summary>
        public static string IconUrl { get; set; }

        /// <summary>来源平台（Modrinth / CurseForge）</summary>
        public static string Source { get; set; }

        /// <summary>项目 ID</summary>
        public static string ProjectId { get; set; }

        /// <summary>返回时导航的目标页面路由名</summary>
        public static string BackPage { get; set; } = "ModsPage";

        /// <summary>已安装的游戏版本列表</summary>
        public static List<string> InstalledVersions { get; set; }

        /// <summary>Minecraft 根目录</summary>
        public static string MinecraftPath { get; set; }

        /// <summary>当前启动器选中的游戏版本</summary>
        public static string CurrentGameVersion { get; set; }

        /// <summary>目标下载子目录名（mods / resourcepacks / shaderpacks 等）</summary>
        public static string TargetSubDir { get; set; } = "mods";

        /// <summary>清空数据</summary>
        public static void Clear()
        {
            Name = null;
            Description = null;
            Author = null;
            Downloads = null;
            IconUrl = null;
            Source = null;
            ProjectId = null;
            BackPage = "ModsPage";
            InstalledVersions = null;
            MinecraftPath = null;
            CurrentGameVersion = null;
            TargetSubDir = "mods";
        }
    }
}
