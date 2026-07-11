using System;
using System.IO;
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
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int ExitCode { get; set; }
        public DateTime LaunchTime { get; set; }

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

            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine($"  报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("═══════════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>获取最近启动日志的最后 200 行（用于 AI 分析）</summary>
        public string GetErrorSummary(int maxLines = 200)
        {
            string content = GetLogContent();
            if (string.IsNullOrEmpty(content)) return "无日志内容";

            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var errorLines = new System.Collections.Generic.List<string>();

            foreach (var line in lines)
            {
                var lower = line.ToLowerInvariant();
                if (lower.Contains("error") || lower.Contains("exception") || lower.Contains("fail")
                    || lower.Contains("crash") || lower.Contains("fatal") || lower.Contains("错误")
                    || lower.Contains("异常") || lower.Contains("失败"))
                {
                    errorLines.Add(line);
                }
            }

            if (errorLines.Count == 0)
            {
                // 取最后 maxLines 行
                for (int i = Math.Max(0, lines.Length - maxLines); i < lines.Length; i++)
                    errorLines.Add(lines[i]);
            }

            return string.Join("\n", errorLines);
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
