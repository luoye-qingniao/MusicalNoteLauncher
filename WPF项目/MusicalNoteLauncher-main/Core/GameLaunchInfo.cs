using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace MusicalNoteLauncher.Core
{
    /// <summary>游戏启动结果详情</summary>
    public class GameLaunchInfo
    {
        public string VersionId { get; set; }
        public string Username { get; set; }
        public string Memory { get; set; }
        public string Resolution { get; set; }
        public string JavaPath { get; set; }
        public string LogFilePath { get; set; }
        /// <summary>Minecraft目录路径（用于查找crash-reports等）</summary>
        public string MinecraftPath { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int ExitCode { get; set; }
        public DateTime LaunchTime { get; set; }

        /// <summary>启动器实时捕获的最近日志（来自GameLauncher的LatestLog队列）</summary>
        public IReadOnlyList<string> LatestLogs { get; set; }

        public GameLaunchInfo()
        {
            LaunchTime = DateTime.Now;
        }

        /// <summary>生成导出报告</summary>
        public string ExportReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  MNL 音符启动器 - 游戏启动报告");
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine($"  启动时间: {LaunchTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  启动结果: {(IsSuccess ? "成功" : "失败")}");
            sb.AppendLine($"  游戏版本: {VersionId}");
            sb.AppendLine($"  玩家名称: {Username}");
            sb.AppendLine($"  分配内存: {Memory}");
            sb.AppendLine($"  游戏分辨率: {Resolution}");
            sb.AppendLine($"  Java 路径: {JavaPath}");
            sb.AppendLine();
            sb.AppendLine("── 系统信息 ──");
            sb.AppendLine($"  操作系统: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"  系统架构: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"  框架版本: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"  处理器数: {Environment.ProcessorCount}");
            sb.AppendLine();

            if (!IsSuccess)
            {
                sb.AppendLine("── 错误信息 ──");
                sb.AppendLine($"  退出码: {ExitCode}");
                if (!string.IsNullOrEmpty(ErrorMessage))
                {
                    sb.AppendLine($"  错误描述: {ErrorMessage}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("── 启动日志 ──");
            string logContent = GetLogContent();
            if (!string.IsNullOrEmpty(logContent))
            {
                sb.AppendLine(logContent);
            }
            else
            {
                sb.AppendLine("  (无日志内容)");
            }

            // 附加崩溃报告
            var crashReport = GetCrashReport();
            if (!string.IsNullOrEmpty(crashReport))
            {
                sb.AppendLine();
                sb.AppendLine("── 崩溃报告 ──");
                sb.AppendLine(crashReport);
            }

            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine($"  报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("═══════════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>
        /// 获取综合错误摘要——多源日志收集（参照PCL CrCollect）
        /// 优先级：crash-reports > latest.log > 启动器实时捕获 > 启动器日志
        /// </summary>
        public string GetErrorSummary(int maxLines = 200)
        {
            var sources = new List<string>();

            // 1. 崩溃报告（最高优先级，参照PCL）
            string crashReport = GetCrashReport();
            if (!string.IsNullOrEmpty(crashReport))
            {
                // 取崩溃报告的前150行（含关键堆栈信息）
                var crashLines = crashReport.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                sources.Add($"=== Minecraft崩溃报告 (前{Math.Min(crashLines.Length, 150)}行) ===");
                for (int i = 0; i < Math.Min(crashLines.Length, 150); i++)
                    sources.Add(crashLines[i]);
                sources.Add("");
            }

            // 2. latest.log（参照PCL）
            string latestLog = GetLatestLog();
            if (!string.IsNullOrEmpty(latestLog))
            {
                var logLines = latestLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                sources.Add($"=== latest.log (最后{Math.Min(logLines.Length, 100)}行) ===");
                int start = Math.Max(0, logLines.Length - 100);
                for (int i = start; i < logLines.Length; i++)
                    sources.Add(logLines[i]);
                sources.Add("");
            }

            // 3. 启动器实时捕获的日志（参照PCL RawOutput）
            if (LatestLogs != null && LatestLogs.Count > 0)
            {
                sources.Add($"=== 启动器实时捕获 (最近{LatestLogs.Count}行) ===");
                foreach (var line in LatestLogs)
                    sources.Add(line);
                sources.Add("");
            }

            // 4. 启动器自身的日志文件
            string launcherLog = GetLogContent();
            if (!string.IsNullOrEmpty(launcherLog))
            {
                var lines = launcherLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var errorLines = new List<string>();
                foreach (var line in lines)
                {
                    var lower = line.ToLowerInvariant();
                    if (lower.Contains("error") || lower.Contains("exception") || lower.Contains("fail")
                        || lower.Contains("crash") || lower.Contains("fatal") || lower.Contains("错误")
                        || lower.Contains("异常") || lower.Contains("失败") || lower.Contains("崩溃")
                        || lower.Contains("warn"))
                    {
                        errorLines.Add(line);
                    }
                }

                if (errorLines.Count > 0)
                {
                    sources.Add($"=== 启动器日志(错误/警告行) ===");
                    sources.AddRange(errorLines.Take(maxLines));
                }
            }

            if (sources.Count == 0) return "无日志内容";

            return string.Join("\n", sources);
        }

        /// <summary>
        /// 获取最新的崩溃报告（参照PCL Collect方法）
        /// 检查 crash-reports/ 目录和 .minecraft 根目录
        /// </summary>
        public string GetCrashReport()
        {
            try
            {
                var candidates = new List<string>();

                // 来源1：crash-reports 文件夹
                string crashDir = Path.Combine(MinecraftPath ?? "", "crash-reports");
                if (Directory.Exists(crashDir))
                {
                    candidates.AddRange(Directory.GetFiles(crashDir, "crash-*.txt"));
                }

                // 来源2：.minecraft 根目录
                if (!string.IsNullOrEmpty(MinecraftPath) && Directory.Exists(MinecraftPath))
                {
                    candidates.AddRange(Directory.GetFiles(MinecraftPath, "crash-*.txt"));
                }

                // 来源3：版本独立目录下的 crash-reports
                if (!string.IsNullOrEmpty(VersionId) && !string.IsNullOrEmpty(MinecraftPath))
                {
                    string versionCrashDir = Path.Combine(MinecraftPath, "versions", VersionId, "crash-reports");
                    if (Directory.Exists(versionCrashDir))
                    {
                        candidates.AddRange(Directory.GetFiles(versionCrashDir, "crash-*.txt"));
                    }
                }

                if (candidates.Count == 0) return null;

                // 按修改时间排序，取最近3分钟内的
                var now = DateTime.Now;
                var recent = candidates
                    .Select(f => new FileInfo(f))
                    .Where(fi => (now - fi.LastWriteTime).TotalMinutes < 10)
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .ToList();

                // 如果最近10分钟没有，就用所有文件中最新的
                if (recent.Count == 0)
                {
                    recent = candidates
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(fi => fi.LastWriteTime)
                        .Take(1)
                        .ToList();
                }

                if (recent.Count > 0)
                {
                    string content = File.ReadAllText(recent[0].FullName, Encoding.UTF8);

                    // 参照PCL：取前300行 + 后700行
                    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
                    if (lines.Length <= 1000) return content;

                    var sb = new StringBuilder();
                    int headLines = Math.Min(300, lines.Length);
                    int tailLines = Math.Min(700, lines.Length - headLines);

                    for (int i = 0; i < headLines; i++)
                        sb.AppendLine(lines[i]);
                    sb.AppendLine("... (中间省略) ...");
                    for (int i = lines.Length - tailLines; i < lines.Length; i++)
                        sb.AppendLine(lines[i]);

                    return sb.ToString();
                }
            }
            catch { }
            return null;
        }

        /// <summary>获取 latest.log（参照PCL）</summary>
        public string GetLatestLog()
        {
            try
            {
                string logsDir = Path.Combine(MinecraftPath ?? "", "logs");
                if (!Directory.Exists(logsDir)) return null;

                string latestLogPath = Path.Combine(logsDir, "latest.log");
                if (!File.Exists(latestLogPath)) return null;

                // 参照PCL：取头1500行 + 尾500行
                string content = File.ReadAllText(latestLogPath, Encoding.UTF8);
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

                if (lines.Length <= 2000) return content;

                var sb = new StringBuilder();
                int headLines = Math.Min(1500, lines.Length);
                int tailLines = Math.Min(500, lines.Length - headLines);

                for (int i = 0; i < headLines; i++)
                    sb.AppendLine(lines[i]);

                if (tailLines > 0)
                {
                    sb.AppendLine("... (中间省略) ...");
                    for (int i = lines.Length - tailLines; i < lines.Length; i++)
                        sb.AppendLine(lines[i]);
                }

                return sb.ToString();
            }
            catch { }
            return null;
        }

        private string GetLogContent()
        {
            try
            {
                if (!string.IsNullOrEmpty(LogFilePath) && File.Exists(LogFilePath))
                {
                    return File.ReadAllText(LogFilePath, Encoding.UTF8);
                }
            }
            catch { }
            return null;
        }
    }
}
