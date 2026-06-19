using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    public static class ModLoaderDetector
    {
        public enum ModLoaderType
        {
            None,
            Forge,
            Fabric,
            Quilt,
            NeoForge
        }

        public class LoaderInfo
        {
            public ModLoaderType Type { get; set; }
            public string VersionId { get; set; }
            public string MinecraftVersion { get; set; }
            public string LoaderVersion { get; set; }
            public string DisplayName { get; set; }
        }

        /// <summary>
        /// 扫描 .minecraft/versions 下所有版本目录，找出所有可用的加载器版本。
        /// 如果提供了 minecraftVersion，则只返回与其匹配的加载器。
        /// </summary>
        public static List<LoaderInfo> DetectAllLoaders(string minecraftPath, string minecraftVersion = null)
        {
            List<LoaderInfo> results = new List<LoaderInfo>();

            string versionsDir = Path.Combine(minecraftPath, "versions");
            if (!Directory.Exists(versionsDir)) return results;

            foreach (string versionDir in Directory.GetDirectories(versionsDir))
            {
                string versionId = Path.GetFileName(versionDir);
                string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
                if (!File.Exists(jsonPath)) continue;

                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        JsonElement root = doc.RootElement;

                        string id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? versionId : versionId;
                        string inheritsFrom = root.TryGetProperty("inheritsFrom", out var inhEl) ? inhEl.GetString() : null;
                        string mainClass = root.TryGetProperty("mainClass", out var mcEl) ? mcEl.GetString() ?? string.Empty : string.Empty;

                        // 这个版本"实际运行"的 MC 版本：有 inheritsFrom 就是父版本，否则等于自身 id
                        string effectiveMcVersion = !string.IsNullOrEmpty(inheritsFrom) ? inheritsFrom : id;

                        // 如果调用方指定了 MC 版本，只返回匹配的
                        if (!string.IsNullOrEmpty(minecraftVersion)
                            && !effectiveMcVersion.Equals(minecraftVersion, StringComparison.OrdinalIgnoreCase)
                            && !id.Equals(minecraftVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // 通过 mainClass / libraries 识别加载器类型
                        ModLoaderType type = ModLoaderType.None;

                        if (mainClass.IndexOf("fabric", StringComparison.OrdinalIgnoreCase) >= 0)
                            type = ModLoaderType.Fabric;
                        else if (mainClass.IndexOf("neoforge", StringComparison.OrdinalIgnoreCase) >= 0)
                            type = ModLoaderType.NeoForge;
                        else if (mainClass.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0)
                            type = ModLoaderType.Forge;
                        else if (mainClass.IndexOf("quilt", StringComparison.OrdinalIgnoreCase) >= 0)
                            type = ModLoaderType.Quilt;

                        if (type == ModLoaderType.None && root.TryGetProperty("libraries", out var libsEl)
                            && libsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement lib in libsEl.EnumerateArray())
                            {
                                if (!lib.TryGetProperty("name", out var nameEl)) continue;
                                string libName = nameEl.GetString() ?? string.Empty;

                                if (libName.IndexOf("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase) >= 0)
                                { type = ModLoaderType.Fabric; break; }
                                if (libName.IndexOf("net.neoforged:neoforge", StringComparison.OrdinalIgnoreCase) >= 0)
                                { type = ModLoaderType.NeoForge; break; }
                                if (libName.IndexOf("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase) >= 0
                                    || libName.IndexOf("cpw.mods:bootstraplauncher", StringComparison.OrdinalIgnoreCase) >= 0
                                    || libName.IndexOf("cpw.mods:securejarhandler", StringComparison.OrdinalIgnoreCase) >= 0)
                                { type = ModLoaderType.Forge; break; }
                                if (libName.IndexOf("org.quiltmc:quilt-loader", StringComparison.OrdinalIgnoreCase) >= 0)
                                { type = ModLoaderType.Fabric; break; }
                            }
                        }

                        // 没识别出加载器 -> 跳过
                        if (type == ModLoaderType.None) continue;

                        // 尝试从目录名提取加载器版本 (如 "fabric-loader-0.19.3-1.20.1")
                        string loaderVersion = null;
                        string[] parts = id.Split('-');
                        if (parts.Length >= 3 && (parts[0].Equals("fabric", StringComparison.OrdinalIgnoreCase)
                                                || parts[0].Equals("forge", StringComparison.OrdinalIgnoreCase)
                                                || parts[0].Equals("neoforge", StringComparison.OrdinalIgnoreCase)
                                                || parts[0].Equals("quilt", StringComparison.OrdinalIgnoreCase)))
                        {
                            // pattern: "fabric-loader-0.19.3-1.20.1"
                            // pattern: "1.20.1-forge-47.3.0"
                            int mcVerIdx = Array.FindIndex(parts, p => p == effectiveMcVersion || p == minecraftVersion);
                            if (mcVerIdx > 0 && mcVerIdx < parts.Length)
                            {
                                List<string> loaderParts = new List<string>();
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (i == mcVerIdx) break;
                                    if (parts[i] == "loader" || parts[i] == "forge" || parts[i] == "neoforge"
                                        || parts[i] == "fabric" || parts[i] == "quilt") continue;
                                    loaderParts.Add(parts[i]);
                                }
                                if (loaderParts.Count > 0) loaderVersion = string.Join(".", loaderParts);
                            }
                            if (string.IsNullOrEmpty(loaderVersion) && parts.Length >= 3)
                            {
                                // fallback: 取第一个看起来像 "x.y.z" 的部分
                                foreach (string p in parts)
                                {
                                    int dots = 0;
                                    bool ok = true;
                                    foreach (char c in p)
                                    {
                                        if (c == '.') dots++;
                                        else if (!char.IsDigit(c)) { ok = false; break; }
                                    }
                                    if (ok && dots >= 1) { loaderVersion = p; break; }
                                }
                            }
                        }

                        results.Add(new LoaderInfo
                        {
                            Type = type,
                            VersionId = id,
                            MinecraftVersion = effectiveMcVersion,
                            LoaderVersion = loaderVersion,
                            DisplayName = GetLoaderDisplayName(type)
                                + (!string.IsNullOrEmpty(loaderVersion) ? " " + loaderVersion : string.Empty)
                                + " (Minecraft " + effectiveMcVersion + ")"
                        });
                    }
                }
                catch
                {
                    // 解析失败，跳过
                }
            }

            return results;
        }

        /// <summary>
        /// 兼容原接口：检查给定版本是否带有加载器（包括继承的）。
        /// </summary>
        public static ModLoaderType DetectModLoader(string minecraftPath, string versionId)
        {
            List<LoaderInfo> infos = DetectAllLoaders(minecraftPath, versionId);
            if (infos.Count == 0) return ModLoaderType.None;
            return infos[0].Type;
        }

        public static string GetLoaderDisplayName(ModLoaderType loaderType)
        {
            switch (loaderType)
            {
                case ModLoaderType.Forge: return "Forge";
                case ModLoaderType.Fabric: return "Fabric";
                case ModLoaderType.Quilt: return "Quilt";
                case ModLoaderType.NeoForge: return "NeoForge";
                default: return "无";
            }
        }
    }
}
