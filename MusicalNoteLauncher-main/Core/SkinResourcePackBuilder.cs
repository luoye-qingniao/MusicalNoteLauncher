using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// PCL 风格的皮肤资源包构建器
    ///
    /// 原理: 把玩家的自定义皮肤 PNG 打包成一个符合 Minecraft 资源包格式的 ZIP 文件,
    /// 替换默认的 Steve/Alex 贴图。游戏启动前放入 resourcepacks 目录并在 options.txt
    /// 中启用, 无需任何 Mod 即可在任何版本显示皮肤。
    ///
    /// 参考: Plain Craft Launcher 2 (PCL2) 中 ModLaunch.vb 的皮肤资源包逻辑。
    /// </summary>
    public static class SkinResourcePackBuilder
    {
        public const string PackName = "MNL Skin";
        public const string PackFileName = "MNL Skin.zip";

        // 1.19.3+ 版本中 Minecraft 的 9 个默认角色名
        private static readonly string[] PlayerNames_Wide = { "steve", "ari", "kai", "makena", "noor" };
        private static readonly string[] PlayerNames_Slim = { "alex", "efe", "sunny", "zuri" };

        /// <summary>
        /// 根据 Minecraft 版本号获取 pack_format
        /// </summary>
        public static int GetPackFormat(string versionId)
        {
            var normalized = NormalizeVersion(versionId);
            if (normalized == null) return 9; // 默认 1.19

            // 根据 PCL 逻辑和 Minecraft Wiki 映射
            if (normalized[0] <= 1 && normalized.Length >= 2)
            {
                int minor = normalized[1];
                int patch = normalized.Length >= 3 ? normalized[2] : 0;

                if (normalized[0] == 0) return 1;
                if (normalized[0] == 1)
                {
                    if (minor <= 8) return 1;
                    if (minor <= 10) return 2;
                    if (minor <= 12) return 3;
                    if (minor <= 14) return 4;
                    if (minor == 15) return 5;
                    if (minor == 16) return patch < 2 ? 5 : 6;
                    if (minor == 17) return 7;
                    if (minor == 18) return 8;
                    if (minor == 19)
                    {
                        if (patch < 3) return 9;
                        if (patch < 4) return 12;
                        return 13;
                    }
                    if (minor == 20)
                    {
                        if (patch < 2) return 15;
                        if (patch < 3) return 18;
                        if (patch < 5) return 22;
                        return 32;
                    }
                    if (minor == 21)
                    {
                        if (patch < 2) return 34;
                        return 42;
                    }
                    return 46; // 1.22+
                }
            }
            return 46; // 未来版本, 用最高值
        }

        /// <summary>
        /// 构建皮肤资源包 ZIP 文件到 gameDir\resourcepacks\MNL Skin.zip
        /// </summary>
        /// <param name="skinSourcePath">皮肤 PNG 源文件路径</param>
        /// <param name="isSlim">是否为 Alex 细手臂模型</param>
        /// <param name="gameDir">游戏目录 ({exe}\.minecraft)</param>
        /// <param name="versionId">Minecraft 版本号 (用于 pack_format)</param>
        /// <returns>是否成功</returns>
        public static bool Build(string skinSourcePath, bool isSlim, string gameDir, string versionId)
        {
            try
            {
                if (!File.Exists(skinSourcePath))
                {
                    Logger.Warning($"[SkinPack] 皮肤文件不存在: {skinSourcePath}");
                    return false;
                }

                int packFormat = GetPackFormat(versionId);
                string resourcepacksDir = Path.Combine(gameDir, "resourcepacks");
                Directory.CreateDirectory(resourcepacksDir);
                string zipPath = Path.Combine(resourcepacksDir, PackFileName);

                // 读取皮肤数据
                byte[] skinData = File.ReadAllBytes(skinSourcePath);

                // 检测版本是否 >= 1.19.3 (需要 9 角色支持)
                bool useMultiPlayer = IsVersionAtLeast(versionId, 1, 19, 3);

                using (var zip = new MemoryStream())
                {
                    using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
                    {
                        // pack.mcmeta
                        string mcmeta = $@"{{
  ""pack"": {{
    ""pack_format"": {packFormat},
    ""description"": ""{PackName}""
  }}
}}";
                        AddEntry(archive, "pack.mcmeta", Encoding.UTF8.GetBytes(mcmeta));

                        // pack.png (64x64 纯色缩略图, 使用皮肤头像区左上 8x8 放大)
                        byte[] packIcon = CreatePackIcon(skinData);
                        if (packIcon != null)
                        {
                            AddEntry(archive, "pack.png", packIcon);
                        }

                        if (useMultiPlayer)
                        {
                            // 1.19.3+: 为所有 9 个默认角色写入, 确保任何名字都能加载皮肤
                            var names = isSlim ? PlayerNames_Slim : PlayerNames_Wide;
                            string modelDir = isSlim ? "slim" : "wide";
                            foreach (string name in names)
                            {
                                string path = $"assets/minecraft/textures/entity/player/{modelDir}/{name}.png";
                                AddEntry(archive, path, skinData);
                            }
                            // 另一模型也写入 (以防万一)
                            var otherNames = isSlim ? PlayerNames_Wide : PlayerNames_Slim;
                            string otherModelDir = isSlim ? "wide" : "slim";
                            foreach (string name in otherNames)
                            {
                                string path = $"assets/minecraft/textures/entity/player/{otherModelDir}/{name}.png";
                                AddEntry(archive, path, skinData);
                            }
                        }
                        else
                        {
                            // 旧版本: 只写 steve.png 或 alex.png
                            if (isSlim)
                            {
                                AddEntry(archive, "assets/minecraft/textures/entity/alex.png", skinData);
                            }
                            else
                            {
                                AddEntry(archive, "assets/minecraft/textures/entity/steve.png", skinData);
                            }
                        }
                    }

                    // 写入磁盘
                    File.WriteAllBytes(zipPath, zip.ToArray());
                }

                Logger.Info($"[SkinPack] 资源包已构建: {zipPath} (pack_format={packFormat}, slim={isSlim})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[SkinPack] 构建资源包失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将 MNL Skin.zip 注入到 options.txt 的资源包列表
        /// 确保启动游戏时自动启��该资源包
        /// </summary>
        /// <param name="gameDir">游戏目录</param>
        /// <returns>是否成功</returns>
        public static bool InjectToOptions(string gameDir)
        {
            try
            {
                string optionsPath = Path.Combine(gameDir, "options.txt");
                if (!File.Exists(optionsPath))
                {
                    // options.txt 尚不存��, 无需注入
                    Logger.Info("[SkinPack] options.txt 不存在, 跳过资源包注入");
                    return false;
                }

                // 检查资源包文件是否存在
                string packPath = Path.Combine(gameDir, "resourcepacks", PackFileName);
                if (!File.Exists(packPath))
                {
                    Logger.Warning("[SkinPack] 资源包文件不存在, 跳过注入");
                    return false;
                }

                string[] lines = File.ReadAllLines(optionsPath, Encoding.UTF8);
                bool modified = false;
                string packEntry = $"file/{PackFileName}";

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("resourcePacks:"))
                    {
                        string value = lines[i].Substring("resourcePacks:".Length).Trim();

                        // 移除旧版本的 "MNL Skin.zip" (不带 file/ 前缀)
                        if (value.Contains($"\"{PackFileName}\"") && !value.Contains(packEntry))
                        {
                            value = value.Replace($"\"{PackFileName}\"", "").Replace("[,", "[").Replace("[,", "[");
                        }

                        // 如果已存在, 跳过
                        if (value.Contains(packEntry))
                        {
                            Logger.Info("[SkinPack] 资源包已在 options.txt 中, 跳过");
                            return true;
                        }

                        // 插入到资源包列表开头
                        if (value.StartsWith("[") && value.EndsWith("]"))
                        {
                            string inner = value.Substring(1, value.Length - 2).Trim();
                            if (!string.IsNullOrEmpty(inner))
                            {
                                value = $"[{packEntry}, {inner}]";
                            }
                            else
                            {
                                value = $"[{packEntry}]";
                            }
                        }
                        else
                        {
                            value = $"[{packEntry}]";
                        }

                        lines[i] = $"resourcePacks:{value}";
                        modified = true;
                    }
                }

                if (modified)
                {
                    File.WriteAllLines(optionsPath, lines, Encoding.UTF8);
                    Logger.Info($"[SkinPack] 已将 {packEntry} 注入 options.txt");
                }

                return modified;
            }
            catch (Exception ex)
            {
                Logger.Error($"[SkinPack] 注入 options.txt 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动游戏后, 从 options.txt 中清理 MNL Skin.zip
        /// 这样可以避免下次用不同账号启动时看到错误的皮肤
        /// </summary>
        public static void CleanFromOptions(string gameDir)
        {
            try
            {
                string optionsPath = Path.Combine(gameDir, "options.txt");
                if (!File.Exists(optionsPath)) return;

                string[] lines = File.ReadAllLines(optionsPath, Encoding.UTF8);
                bool modified = false;
                string packEntry = $"file/{PackFileName}";

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("resourcePacks:"))
                    {
                        string value = lines[i].Substring("resourcePacks:".Length).Trim();
                        if (value.Contains(packEntry) || value.Contains($"\"{PackFileName}\""))
                        {
                            // 移除 MNL Skin.zip
                            value = value.Replace($"{packEntry}, ", "")
                                       .Replace($", {packEntry}", "")
                                       .Replace(packEntry, "")
                                       .Replace($"\"{PackFileName}\", ", "")
                                       .Replace($", \"{PackFileName}\"", "")
                                       .Replace($"\"{PackFileName}\"", "");
                            // 清理残留的逗号和空格
                            value = value.Replace("[,", "[").Replace(" ,", ",").Replace(", ]", "]");
                            lines[i] = $"resourcePacks:{value}";
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    File.WriteAllLines(optionsPath, lines, Encoding.UTF8);
                    Logger.Info("[SkinPack] 已从 options.txt 清理 MNL Skin 资源包");
                }
            }
            catch { }
        }

        #region 辅助方法

        private static void AddEntry(ZipArchive archive, string entryName, byte[] data)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// 从皮肤 PNG 头部 (8x8 区域) 创建 64x64 的资源包图标
        /// </summary>
        private static byte[] CreatePackIcon(byte[] skinData)
        {
            try
            {
                using (var ms = new MemoryStream(skinData))
                {
                    var decoder = System.Windows.Media.Imaging.PngBitmapDecoder.Create(
                        ms,
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];

                    // 裁剪头像区域 (8,8)-(16,16) 并放大到 64x64
                    var cropped = new System.Windows.Media.Imaging.CroppedBitmap(
                        frame,
                        new System.Windows.Int32Rect(8, 8, 8, 8));

                    var scaled = new System.Windows.Media.Imaging.TransformedBitmap(
                        cropped,
                        new System.Windows.Media.ScaleTransform(8, 8));

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled));
                    using (var outMs = new MemoryStream())
                    {
                        encoder.Save(outMs);
                        return outMs.ToArray();
                    }
                }
            }
            catch
            {
                return null; // 失败了就算了, pack.png 不是必需的
            }
        }

        /// <summary>
        /// 解析版本号字符串 (如 "1.19.4" → [1, 19, 4])
        /// </summary>
        private static int[] NormalizeVersion(string versionId)
        {
            if (string.IsNullOrEmpty(versionId)) return null;
            var match = Regex.Match(versionId, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success) return null;

            var parts = new List<int>();
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (int.TryParse(match.Groups[i].Value, out int n))
                    parts.Add(n);
            }
            return parts.ToArray();
        }

        /// <summary>
        /// 检查版本是否 >= 指定版本号
        /// </summary>
        private static bool IsVersionAtLeast(string versionId, int major, int minor, int patch)
        {
            var v = NormalizeVersion(versionId);
            if (v == null) return false;

            int a = v[0];
            int b = v.Length >= 2 ? v[1] : 0;
            int c = v.Length >= 3 ? v[2] : 0;

            if (a != major) return a > major;
            if (b != minor) return b > minor;
            return c >= patch;
        }

        #endregion
    }
}
