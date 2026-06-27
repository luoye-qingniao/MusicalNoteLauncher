using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 搜索匹配工具类，移植自 PCL 的 SearchSimilarity 模糊匹配算法。
    /// 借鉴 PCL 的 ModBase.vb 中的搜索实现。
    /// </summary>
    public static class SearchHelper
    {
        #region 核心算法 —— 移植自 PCL ModBase.vb

        /// <summary>
        /// 获取搜索文本的相似度。模仿 PCL 的 SearchSimilarity。
        /// 贪婪扫描源字符串中与查询匹配的字符序列，对长连续匹配加权。
        /// </summary>
        public static double SearchSimilarity(string source, string query)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query)) return 0;

            var str = new StringBuilder(source.Length);
            str.Append(source.ToLowerInvariant().Replace(" ", ""));
            query = query.ToLowerInvariant().Replace(" ", "");
            int sourceLength = str.Length, queryLength = query.Length;
            if (queryLength == 0) return 0;

            int qp = 0;
            double lenSum = 0;

            while (qp < queryLength)
            {
                int sp = 0, lenMax = 0, spMax = 0;
                int currentSourceLength = str.Length;
                while (sp < currentSourceLength)
                {
                    int len = 0;
                    while ((qp + len) < queryLength && (sp + len) < currentSourceLength
                           && str[sp + len] == query[qp + len])
                    {
                        len++;
                    }
                    if (len > lenMax)
                    {
                        lenMax = len;
                        spMax = sp;
                    }
                    sp += (len > 0) ? len : 1;
                }
                if (lenMax > 0)
                {
                    str.Remove(spMax, lenMax);
                    double incWeight = Math.Pow(1.4, 3 + lenMax) - 3.6;          // 长度加成
                    incWeight *= 1 + 0.3 * Math.Max(0, 3 - Math.Abs(qp - spMax)); // 位置加成
                    lenSum += incWeight;
                }
                qp += (lenMax > 0) ? lenMax : 1;
            }

            return (lenSum / queryLength)
                   * (3 / Math.Sqrt(sourceLength + 15))
                   * (queryLength <= 2 ? 3 - queryLength : 1);
        }

        /// <summary>
        /// 获取多段文本加权后的相似度。模仿 PCL 的 SearchSimilarityWeighted。
        /// </summary>
        public static double SearchSimilarityWeighted(List<SearchSource> sources, string query)
        {
            double totalWeight = 0;
            double sum = 0;
            foreach (var pair in sources)
            {
                if (pair.Aliases != null && pair.Aliases.Length > 0)
                {
                    sum += pair.Aliases.Max(a => SearchSimilarity(a, query)) * pair.Weight;
                }
                totalWeight += pair.Weight;
            }
            return totalWeight > 0 ? sum / totalWeight : 0;
        }

        /// <summary>
        /// 进行多段文本加权搜索。模仿 PCL 的 Search(Of T)。
        /// </summary>
        public static List<SearchEntry<T>> Search<T>(List<SearchEntry<T>> entries, string query,
            int maxBlurCount = 5, double minBlurSimilarity = 0.1)
        {
            var resultList = new List<SearchEntry<T>>();
            if (entries == null || entries.Count == 0) return resultList;

            if (string.IsNullOrWhiteSpace(query))
            {
                resultList.AddRange(entries);
                return resultList;
            }

            var queryParts = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                entry.Similarity = SearchSimilarityWeighted(entry.SearchSource, query);
                entry.AbsoluteRight =
                    queryParts.All(qp =>
                        entry.SearchSource.Any(src =>
                            src.Aliases.Any(a =>
                                a.Replace(" ", "").IndexOf(qp.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0)));
            }

            // 完全匹配项排在前面
            var absoluteRightItems = entries.Where(e => e.AbsoluteRight).ToList();
            resultList.AddRange(absoluteRightItems);

            // 模糊匹配项按相似度排序
            var blurItems = entries
                .Where(e => !e.AbsoluteRight && e.Similarity >= minBlurSimilarity)
                .OrderByDescending(e => e.Similarity)
                .Take(maxBlurCount)
                .ToList();
            resultList.AddRange(blurItems);

            return resultList;
        }

        #endregion

        #region 简便接口 —— 用于简单列表过滤

        /// <summary>
        /// 简便匹配：检查项目是否通过模糊搜索。若查询为空则全部通过。
        /// 使用加权相似度（名称权重 1，描述权重 0.5，其余权重 0.3），阈值 0.1。
        /// </summary>
        public static bool IsMatch(string query, string name, string description, params string[] extraFields)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            var sources = new List<SearchSource>
            {
                new SearchSource(name, 1),
                new SearchSource(description, 0.5)
            };
            foreach (var field in extraFields)
            {
                if (!string.IsNullOrEmpty(field))
                    sources.Add(new SearchSource(field, 0.3));
            }

            return SearchSimilarityWeighted(sources, query) >= 0.1;
        }

        /// <summary>
        /// 简便匹配：仅检查名称。阈值降低到 0.05 以匹配短查询（如版本号）。
        /// </summary>
        public static bool IsMatchSimple(string query, params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            var sources = fields
                .Where(f => !string.IsNullOrEmpty(f))
                .Select((f, i) => new SearchSource(f, i == 0 ? 1 : 0.5))
                .ToList();

            return sources.Count > 0 && SearchSimilarityWeighted(sources, query) >= 0.05;
        }

        #endregion
    }

    /// <summary>
    /// 用于搜索的项目。模仿 PCL 的 SearchEntry(Of T)。
    /// </summary>
    public class SearchEntry<T>
    {
        public T Item { get; set; }
        public List<SearchSource> SearchSource { get; set; } = new List<SearchSource>();
        public double Similarity { get; set; }
        public bool AbsoluteRight { get; set; }

        public override string ToString() => $"{Math.Round(Similarity, 3)} - {Item}";
    }

    /// <summary>
    /// 单个用于搜索的文本源。模仿 PCL 的 SearchSource。
    /// </summary>
    public class SearchSource
    {
        public string[] Aliases { get; set; }
        public double Weight { get; set; }

        public SearchSource() { }

        public SearchSource(string text, double weight = 1)
        {
            Aliases = new[] { text ?? "" };
            Weight = weight;
        }

        public SearchSource(string[] aliases, double weight = 1)
        {
            Aliases = aliases;
            Weight = weight;
        }
    }
}
