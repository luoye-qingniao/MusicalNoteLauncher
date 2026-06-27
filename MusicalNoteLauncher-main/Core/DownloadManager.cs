using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 统一的下载信息模型
    /// </summary>
    public class DownloadVersionInfo
    {
        public string VersionName { get; set; }
        public string DisplayInfo { get; set; }
        public string VersionType { get; set; }
        public bool IsRecommended { get; set; }
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public object SourceData { get; set; }
    }

    /// <summary>
    /// 统一的下载管理器 - 纯代码方式，不使用任何 Window/Dialog 弹窗
    /// </summary>
    public static class DownloadManager
    {
        private static readonly ModrinthApiService _modrinthApi = new ModrinthApiService();
        private static readonly CurseForgeApiService _curseForgeApi = new CurseForgeApiService();

        /// <summary>
        /// 从 Modrinth 获取项目版本列表
        /// </summary>
        public static async Task<List<DownloadVersionInfo>> GetModrinthVersions(string projectId, string gameVersion = null)
        {
            var versions = await _modrinthApi.GetProjectVersions(projectId, gameVersion);
            var result = new List<DownloadVersionInfo>();

            foreach (var v in versions)
            {
                if (v.Files == null || v.Files.Count == 0 || string.IsNullOrEmpty(v.DownloadUrl))
                    continue;

                result.Add(new DownloadVersionInfo
                {
                    VersionName = v.DisplayName,
                    DisplayInfo = $"支持版本: {v.DisplayGameVersions} | 加载器: {v.DisplayLoaders}",
                    VersionType = v.VersionTypeText,
                    IsRecommended = v.IsRecommended,
                    DownloadUrl = v.DownloadUrl,
                    FileName = v.FileName,
                    FileSize = v.FileSize,
                    SourceData = v
                });
            }

            return result;
        }

        /// <summary>
        /// 从 CurseForge 获取项目文件列表
        /// </summary>
        public static async Task<List<DownloadVersionInfo>> GetCurseForgeVersions(long modId)
        {
            var versions = await _curseForgeApi.GetModFiles(modId);
            var result = new List<DownloadVersionInfo>();

            foreach (var v in versions)
            {
                if (string.IsNullOrEmpty(v.DownloadUrl))
                    continue;

                result.Add(new DownloadVersionInfo
                {
                    VersionName = v.VersionName,
                    DisplayInfo = $"支持版本: {v.DisplayGameVersions}",
                    VersionType = v.VersionTypeText,
                    IsRecommended = v.IsRecommended,
                    DownloadUrl = v.DownloadUrl,
                    FileName = v.FileNameSafe,
                    FileSize = v.FileSize,
                    SourceData = v
                });
            }

            return result;
        }

        /// <summary>
        /// 获取推荐版本 - 优先返回第一个 IsRecommended=true 的版本，没有则返回第一个
        /// </summary>
        public static DownloadVersionInfo GetRecommendedVersion(List<DownloadVersionInfo> versions)
        {
            if (versions == null || versions.Count == 0)
                return null;

            for (int i = 0; i < versions.Count; i++)
            {
                if (versions[i].IsRecommended)
                    return versions[i];
            }

            return versions[0];
        }

        /// <summary>
        /// 添加下载任务到下载中心（任务管理器）并开始下载
        /// </summary>
        public static bool AddDownloadTask(string resourceName, string targetDir, DownloadVersionInfo version)
        {
            try
            {
                if (version == null || string.IsNullOrEmpty(version.DownloadUrl))
                {
                    ModernMessageBox.ShowWarning($"[{resourceName}] 无法获取下载链接！", "提示");
                    return false;
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string fileName = version.FileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"{resourceName}_{DateTime.Now:yyyyMMddHHmmss}.jar";
                }

                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                string savePath = Path.Combine(targetDir, fileName);

                var task = new GenericDownloadTaskViewModel(version.DownloadUrl, savePath, fileName);
                DownloadTaskManager.Instance.AddTask(task);
                _ = task.StartDownloadAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddDownloadTask Exception: {ex.Message}");
                ModernMessageBox.ShowError($"添加下载任务失败: {ex.Message}", "错误");
                return false;
            }
        }

        /// <summary>
        /// 简易下载流程：获取版本 -> 自动选推荐版本 -> 开始下载
        /// 适用于按钮事件等无需用户交互选择的场景。完全不使用任何 Window/Dialog。
        /// </summary>
        public static async Task<bool> QuickDownload(string resourceName, string targetDir, string modrinthId, long? curseForgeId = null)
        {
            List<DownloadVersionInfo> versions = null;

            if (!string.IsNullOrEmpty(modrinthId))
            {
                versions = await GetModrinthVersions(modrinthId);
            }
            else if (curseForgeId.HasValue)
            {
                versions = await GetCurseForgeVersions(curseForgeId.Value);
            }

            if (versions == null || versions.Count == 0)
            {
                ModernMessageBox.ShowInfo($"[{resourceName}] 未找到可用的下载版本！", "提示");
                return false;
            }

            var recommended = GetRecommendedVersion(versions);
            if (recommended == null)
            {
                ModernMessageBox.ShowInfo($"[{resourceName}] 未找到可用的下载版本！", "提示");
                return false;
            }

            bool success = AddDownloadTask(resourceName, targetDir, recommended);
            if (success)
            {
                string versionInfo = !string.IsNullOrEmpty(recommended.VersionName) ? $"（版本: {recommended.VersionName}）" : "";
                ModernMessageBox.ShowInfo($"已将 {resourceName}{versionInfo} 添加到下载任务", "提示");
            }

            return success;
        }
    }
}
