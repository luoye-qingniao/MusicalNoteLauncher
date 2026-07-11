using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// Minecraft options.txt 配置文件校验与重置服务
    /// </summary>
    public static class OptionsFileValidator
    {
        /// <summary>校验结果</summary>
        public class ValidationResult
        {
            /// <summary>文件是否存在</summary>
            public bool Exists { get; set; }
            /// <summary>是否包含损坏/非法条目</summary>
            public bool HasBadEntries { get; set; }
            /// <summary>损坏条目的具体内容（最多保留 5 条用于提示）</summary>
            public List<string> BadEntries { get; set; } = new List<string>();
            /// <summary>总行数</summary>
            public int TotalLines { get; set; }
            /// <summary>有效行数</summary>
            public int ValidLines { get; set; }
            /// <summary>损坏行数</summary>
            public int BadLineCount { get; set; }
            /// <summary>是否需要建议重置（损坏行 > 总行数 30% 或 > 10 行）</summary>
            public bool ShouldSuggestReset => !Exists ||
                (TotalLines > 0 && (BadLineCount > 10 || (double)BadLineCount / TotalLines > 0.3));
        }

        /// <summary>options.txt 中必须存在的默认键值对</summary>
        private static readonly Dictionary<string, string> DefaultOptions = new Dictionary<string, string>
        {
            { "lang", "zh_cn" },
            { "fullscreen", "false" },
            { "fov", "70" },
            { "gamma", "0.5" },
            { "renderDistance", "12" },
            { "simulationDistance", "8" },
            { "guiScale", "0" },
            { "particles", "1" },
            { "music", "1.0" },
            { "sound", "1.0" },
            { "difficulty", "2" },
            { "ao", "true" },
            { "skipMultiplayerWarning", "false" },
            { "hideServerAddress", "false" },
            { "autoJump", "true" },
            { "fovEffectScale", "1.0" },
            { "screenEffectScale", "1.0" },
            { "entityDistanceScaling", "1.0" },
            { "maxFps", "120" },
            { "graphicsMode", "0" },
            { "clouds", "1" }
        };

        /// <summary>
        /// 校验 options.txt 文件完整性
        /// </summary>
        /// <param name="optionsPath">options.txt 完整路径</param>
        /// <returns>校验结果</returns>
        public static ValidationResult Validate(string optionsPath)
        {
            var result = new ValidationResult();

            try
            {
                if (!File.Exists(optionsPath))
                {
                    result.Exists = false;
                    Logger.Info($"[配置校验] options.txt 不存在: {optionsPath}");
                    return result;
                }

                result.Exists = true;
                string content = File.ReadAllText(optionsPath, Encoding.UTF8);

                // 检测是否为二进制/乱码内容（高比例非ASCII非中文字符）
                if (DetectGarbledContent(content))
                {
                    result.HasBadEntries = true;
                    result.BadEntries.Add("文件包含大量乱码/二进制内容");
                    result.BadLineCount = int.MaxValue / 2; // 确保触发建议重置
                    result.TotalLines = content.Split('\n').Length;
                    Logger.Warning($"[配置校验] options.txt 疑似乱码文件: {optionsPath}");
                    return result;
                }

                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                result.TotalLines = lines.Length;

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // 有效行格式: key:value
                    int colonIndex = trimmed.IndexOf(':');
                    if (colonIndex <= 0 || colonIndex >= trimmed.Length - 1)
                    {
                        result.BadEntries.Add(trimmed.Length > 60
                            ? trimmed.Substring(0, 60) + "..."
                            : trimmed);
                        result.BadLineCount++;
                        if (result.BadEntries.Count >= 5) break; // 只保留前5条
                        continue;
                    }

                    // 检查 key 是否包含非法字符（只允许字母、数字、下划线、连字符）
                    string key = trimmed.Substring(0, colonIndex);
                    if (key.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
                    {
                        result.BadEntries.Add(trimmed.Length > 60
                            ? trimmed.Substring(0, 60) + "..."
                            : trimmed);
                        result.BadLineCount++;
                        if (result.BadEntries.Count >= 5) break;
                        continue;
                    }

                    result.ValidLines++;
                }

                result.HasBadEntries = result.BadLineCount > 0;

                if (result.HasBadEntries)
                {
                    Logger.Info($"[配置校验] options.txt 包含 {result.BadLineCount} 条损坏项 / {result.TotalLines} 总行");
                }
                else
                {
                    Logger.Info($"[配置校验] options.txt 校验通过 ({result.TotalLines} 行)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[配置校验] 校验 options.txt 时出错: {ex.Message}");
                result.HasBadEntries = true;
                result.BadEntries.Add($"读取失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 备份当前 options.txt 并生成全新的默认配置文件
        /// </summary>
        /// <param name="optionsPath">options.txt 完整路径</param>
        /// <param name="keepExistingValid">是否保留现有文件中的有效行</param>
        /// <returns>操作结果描述</returns>
        public static string ResetWithBackup(string optionsPath, bool keepExistingValid = false)
        {
            try
            {
                string backupPath = null;

                // 备份原文件
                if (File.Exists(optionsPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string dir = Path.GetDirectoryName(optionsPath);
                    backupPath = Path.Combine(dir ?? "", $"options.txt.backup_{timestamp}");
                    File.Copy(optionsPath, backupPath, overwrite: false);
                    Logger.Info($"[配置重置] 已备份原配置到: {backupPath}");
                }

                // 保留现有有效行
                Dictionary<string, string> existingValid = new Dictionary<string, string>();
                if (keepExistingValid && File.Exists(optionsPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(optionsPath);
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;
                            int ci = trimmed.IndexOf(':');
                            if (ci > 0 && ci < trimmed.Length - 1)
                            {
                                string key = trimmed.Substring(0, ci).Trim();
                                string value = trimmed.Substring(ci + 1).Trim();
                                if (!existingValid.ContainsKey(key))
                                    existingValid[key] = value;
                            }
                        }
                    }
                    catch { }
                }

                // 生成新的 options.txt：以默认值为底，叠加现有有效值
                var finalOptions = new Dictionary<string, string>(DefaultOptions);
                foreach (var kvp in existingValid)
                {
                    finalOptions[kvp.Key] = kvp.Value;
                }

                var sb = new StringBuilder();
                foreach (var kvp in finalOptions)
                {
                    sb.AppendLine($"{kvp.Key}:{kvp.Value}");
                }

                string dir2 = Path.GetDirectoryName(optionsPath);
                if (!string.IsNullOrEmpty(dir2))
                    Directory.CreateDirectory(dir2);

                File.WriteAllText(optionsPath, sb.ToString(), Encoding.UTF8);
                Logger.Info($"[配置重置] 已生成新的 options.txt ({finalOptions.Count} 项配置)");
                return backupPath ?? "已生成新配置文件";
            }
            catch (Exception ex)
            {
                Logger.Error($"[配置重置] 重置 options.txt 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检测内容是否为乱码/二进制（高比例非可打印字符或连续 null 字节）
        /// </summary>
        private static bool DetectGarbledContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;

            int totalChars = content.Length;
            int badChars = 0;
            int nullBytes = 0;

            foreach (char c in content)
            {
                if (c == '\0')
                {
                    nullBytes++;
                    badChars++;
                }
                else if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    badChars++;
                }
                // 替换字符 (U+FFFD) 表示编码失败
                else if (c == '\uFFFD')
                {
                    badChars++;
                }
            }

            // 如果 null 字节超过 10% 或 控制字符超过 30%
            if (nullBytes > totalChars * 0.1) return true;
            if (totalChars > 50 && (double)badChars / totalChars > 0.3) return true;

            return false;
        }
    }
}
