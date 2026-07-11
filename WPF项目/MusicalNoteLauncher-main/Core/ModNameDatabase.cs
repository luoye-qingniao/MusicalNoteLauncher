using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// MC 百科 Mod 中文名 ↔ 英文关键词 映射数据库
    ///
    /// 数据来源: PCL2 内置的 moddata.txt (27K+ 条目, 来自 MC百科/CurseForge/Modrinth)
    ///
    /// 格式: curseforge-slug@modrinth-slug|中文1¨alt-slug|中文2
    ///   - @ 分隔 CurseForge 和 Modrinth 的 slug
    ///   - | 后为中文名 (可含括号内的英文名)
    ///   - ¨ 分隔同一 Mod 的多个别名
    ///
    /// 当用户用中文搜索时, 本类将中文关键词转译为英文 slug/关键词,
    /// 再发送给 CurseForge/Modrinth API, 实现中文搜索能力。
    /// </summary>
    public static class ModNameDatabase
    {
        private static bool _loaded;
        private static readonly List<ModEntry> _entries = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 加载 moddata.txt (首次调用自动加载)
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                LoadFromFile();
                _loaded = true;
            }
        }

        /// <summary>
        /// 用中文查询匹配的 Mod, 返回最适合的英文搜索关键词
        ///
        /// 返回:
        ///   - 若找到匹配: 返回英文关键词列表 (API 可直接使用)
        ///   - 若未找到: 返回空列表 (建议提示用户改用英文搜索)
        /// </summary>
        public static List<string> GetEnglishKeywords(string chineseQuery)
        {
            EnsureLoaded();
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(chineseQuery)) return results;

            // 规范化查询
            string q = chineseQuery.Trim().ToLowerInvariant();

            // 检查是否包含中文字符 — 不含则直接返回
            if (!ContainsChinese(q)) return results;

            // 按匹配度排序, 选出最佳的几个
            var scored = new List<(ModEntry entry, int score)>();

            foreach (var entry in _entries)
            {
                int bestScore = 0;
                foreach (var cnName in entry.ChineseNames)
                {
                    int s = MatchScore(cnName, q);
                    if (s > bestScore) bestScore = s;
                }

                if (bestScore > 0)
                {
                    // 增加流行度权重 (数据库中顺序靠前 = 更流行)
                    int popularityBonus = Math.Max(0, 1000 - scored.Count);
                    scored.Add((entry, bestScore + popularityBonus / 100));
                }
            }

            // 按得分降序, 取前 5 个
            var top = scored
                .OrderByDescending(x => x.score)
                .Take(5)
                .Select(x => x.entry)
                .ToList();

            // 收集英文关键词
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in top)
            {
                if (!string.IsNullOrEmpty(entry.CurseForgeSlug))
                    keywords.Add(entry.CurseForgeSlug);
                if (!string.IsNullOrEmpty(entry.ModrinthSlug))
                    keywords.Add(entry.ModrinthSlug);
                if (!string.IsNullOrEmpty(entry.EnglishName))
                    keywords.Add(entry.EnglishName);
            }

            // 去重后取前 3 个关键词 (避免一次发太多短语)
            var result = keywords
                .Where(k => k.Length >= 3)
                .Take(3)
                .ToList();

            return result;
        }

        /// <summary>
        /// 获取最佳单个英文关键词 (用于不支持多关键词的 API)
        /// </summary>
        public static string GetBestEnglishKeyword(string chineseQuery)
        {
            var keywords = GetEnglishKeywords(chineseQuery);
            return keywords.FirstOrDefault();
        }

        /// <summary>
        /// 检测查询中是否包含中文字符
        /// </summary>
        public static bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;   // CJK 统一汉字
                if (c >= 0x3400 && c <= 0x4DBF) return true;   // CJK 扩展 A
                if (c >= 0xF900 && c <= 0xFAFF) return true;   // CJK 兼容汉字
            }
            return false;
        }

        #region 内部实现

        private static void LoadFromFile()
        {
            try
            {
                // 优先从 exe 同目录加载 (方便用户自行更新)
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(exeDir, "moddata.txt");

                if (!File.Exists(filePath))
                {
                    // 回退: 从 Assets 目录加载
                    filePath = Path.Combine(exeDir, "Assets", "moddata.txt");
                }

                if (!File.Exists(filePath))
                {
                    Logger.Warning("[ModNameDB] moddata.txt 未找到, 中文搜索将不可用");
                    return;
                }

                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    var entry = ParseLine(line);
                    if (entry != null)
                        _entries.Add(entry);
                }

                Logger.Info($"[ModNameDB] 已加载 {_entries.Count} 条中文名映射");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ModNameDB] 加载失败: {ex.Message}");
            }
        }

        private static ModEntry ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            // 跳过注释和纯数字行
            line = line.Trim();
            if (line.StartsWith("#") || line.StartsWith("//")) return null;

            // 格式: curseforge-slug@modrinth-slug|中文名
            // 或:   @modrinth-slug|中文名  (无 curseforge)
            // 或:   curseforge-slug@|中文名  (无 modrinth)

            int pipeIdx = line.IndexOf('|');
            if (pipeIdx < 0) return null;

            string slugPart = line.Substring(0, pipeIdx).Trim();
            string namePart = line.Substring(pipeIdx + 1).Trim();

            if (string.IsNullOrEmpty(namePart)) return null;

            // 处理 ¨ 分隔的多个别名
            var aliasParts = namePart.Split('¨');
            var chineseNames = new List<string>();
            string englishName = null;

            foreach (var alias in aliasParts)
            {
                string trimmed = alias.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 提取中文部分和括号内的英文部分
                var parsed = ParseNameAlias(trimmed);
                if (!string.IsNullOrEmpty(parsed.cn))
                    chineseNames.Add(parsed.cn);
                if (!string.IsNullOrEmpty(parsed.en) && englishName == null)
                    englishName = parsed.en;
            }

            if (chineseNames.Count == 0) return null;

            // 解析 slug 部分
            string cfSlug = null;
            string mrSlug = null;

            int atIdx = slugPart.IndexOf('@');
            if (atIdx >= 0)
            {
                if (atIdx > 0) cfSlug = slugPart.Substring(0, atIdx).Trim();
                if (atIdx < slugPart.Length - 1) mrSlug = slugPart.Substring(atIdx + 1).Trim();
            }
            else
            {
                // 无 @ → 直接作为 slug (可能是纯 Modrinth slug)
                mrSlug = slugPart;
            }

            // 清理空字符串
            if (string.IsNullOrEmpty(cfSlug)) cfSlug = null;
            if (string.IsNullOrEmpty(mrSlug)) mrSlug = null;

            return new ModEntry
            {
                CurseForgeSlug = cfSlug,
                ModrinthSlug = mrSlug,
                ChineseNames = chineseNames,
                EnglishName = englishName
            };
        }

        private static (string cn, string en) ParseNameAlias(string alias)
        {
            string cn = null;
            string en = null;

            // 判断是否以英文开头 (ASCII 字符开头) → 这是英文名
            if (alias.Length > 0 && alias[0] < 128)
            {
                en = alias;
                return (null, en);
            }

            // 提取括号内的英文名
            int parenOpen = alias.LastIndexOf('(');
            int parenClose = alias.LastIndexOf(')');
            if (parenOpen >= 0 && parenClose > parenOpen)
            {
                en = alias.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
                cn = alias.Substring(0, parenOpen).Trim();
            }
            else
            {
                cn = alias;
            }

            // 去掉中文名末尾的 * (PCL 的通配符标记)
            if (cn != null && cn.EndsWith("*"))
                cn = cn.Substring(0, cn.Length - 1);

            return (cn, en);
        }

        /// <summary>
        /// 计算查询与中文名的匹配得分
        /// 得分越高越匹配, 0 表示不匹配
        /// </summary>
        private static int MatchScore(string chineseName, string query)
        {
            string name = chineseName.ToLowerInvariant();

            // 完全匹配 → 最高分
            if (name == query) return 1000;

            // 以查询开头 → 高分
            if (name.StartsWith(query)) return 800;

            // 包含查询 → 中分
            if (name.Contains(query)) return 600;

            // 模糊匹配: 查询中的每个字都在中文名中出现
            int matchCount = 0;
            foreach (char c in query)
            {
                if (name.Contains(c))
                    matchCount++;
            }

            if (matchCount == 0) return 0;

            // 匹配字数比例 × 基础分
            double ratio = (double)matchCount / query.Length;
            return (int)(400 * ratio);
        }

        private class ModEntry
        {
            public string CurseForgeSlug;
            public string ModrinthSlug;
            public List<string> ChineseNames;
            public string EnglishName;
        }

        #endregion
    }
}
