using System;
using System.Collections.Generic;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 游戏版本信息
    /// </summary>
    public class VersionInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public DateTime ReleaseTime { get; set; }
        public bool IsDownloaded { get; set; }
        public string DisplayType => Type == "release" ? "正式版" : (Type == "snapshot" ? "快照版" : Type);
    }

    /// <summary>
    /// 下载进度报告器（生产方 Report，消费方订阅 ProgressChanged）
    /// </summary>
    public class DownloadProgress
    {
        public event Action<DownloadProgressInfo> ProgressChanged;

        public void Report(DownloadProgressInfo info)
        {
            ProgressChanged?.Invoke(info);
        }
    }

    /// <summary>
    /// 下载任务配置信息
    /// </summary>
    public class DownloadTaskInfo
    {
        public string VersionId { get; set; }
        public string Status { get; set; }
        public string CurrentFile { get; set; }
        public bool IsCompleted { get; set; }
        public double Progress { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 下载进度信息（供 DownloadProgress.Report 传入）
    /// </summary>
    public class DownloadProgressInfo
    {
        public double Progress { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public string CurrentFile { get; set; }
        public string Status { get; set; }

        // 以下由 VersionRepairService 额外使用
        public string DownloadedSize { get; set; }
        public string TotalSize { get; set; }
        public string Speed { get; set; }
        public double SpeedBytesPerSecond { get; set; }
    }

    /// <summary>
    /// 依赖库下载信息
    /// </summary>
    public class LibraryDownloadInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// 资源索引信息
    /// </summary>
    public class AssetIndexInfo
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    /// 文件大小格式化工具
    /// </summary>
    public static class FileSizeFormatter
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 0) return "0 B";
            int unitIndex = 0;
            double size = bytes;
            while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##} {SizeUnits[unitIndex]}";
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 0) return "0 B/s";
            int unitIndex = 0;
            double speed = bytesPerSecond;
            while (speed >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                speed /= 1024;
                unitIndex++;
            }
            return $"{speed:0.##} {SizeUnits[unitIndex]}/s";
        }
    }
}
