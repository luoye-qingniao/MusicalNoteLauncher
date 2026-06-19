using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public class VersionTypeToIconConverter : SafeConverterBase<VersionTypeToIconConverter>
    {
        // 进程级图标缓存
        private static readonly object _lockObj = new object();
        private static readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>();

        // 资源目录根路径（本地 PNG 所在的目录）
        private const string ResourceDir = "提取的模组加载器图标";

        // 根据 version_manifest.json 中常见字符串做 McInstanceState 映射
        // release → Grass.png (原版)
        // snapshot/pre-release → CommandBlock.png (快照)
        // old_alpha/old_beta → CobbleStone.png (老版本)
        // forge → Anvil.png (Forge)
        // fabric → Fabric.png
        // neoforge → NeoForge.png
        // optifine → GrassPath.png (草径)
        // liteloader → Egg.png
        // fool/fun (愚人节) → GoldBlock.png
        // 其他 → RedstoneBlock.png (错误态)
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string type = (value as string) ?? string.Empty;
            string fileName = ResolveFileName(type);
            return LoadIcon(fileName);
        }

        private static string ResolveFileName(string type)
        {
            if (string.IsNullOrEmpty(type))
                return "RedstoneBlock.png";

            string t = type.Trim().ToLowerInvariant();

            // 优先匹配模组加载器（id 中常带 forge/fabric/...）
            if (t.Contains("neoforge"))
                return "NeoForge.png";
            if (t.Contains("forge"))
                return "Anvil.png";
            if (t.Contains("fabric"))
                return "Fabric.png";
            if (t.Contains("optifine") || t.Contains("optifabric"))
                return "GrassPath.png";
            if (t.Contains("liteloader"))
                return "Egg.png";

            // 版本状态（来自 version_manifest.json 的 type 字段）
            if (t == "release")
                return "Grass.png";
            if (t == "snapshot" || t == "pre-release" || t == "experimental")
                return "CommandBlock.png";
            if (t.StartsWith("old_") || t == "old")
                return "CobbleStone.png";

            // 愚人节版本（23w13a_or_b 等）
            if (t.Contains("fool") || t.Contains("fun") || t.Contains("23w13a") || t.Contains("20w14infinite"))
                return "GoldBlock.png";

            // 兜底：红石块（错误态）
            return "RedstoneBlock.png";
        }

        private static ImageSource LoadIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            lock (_lockObj)
            {
                if (_iconCache.TryGetValue(fileName, out var cached))
                    return cached;
            }

            ImageSource result = null;
            try
            {
                // 先尝试 pack URI（资源文件）
                string packUri = "pack://application:,,,/" + ResourceDir + "/" + fileName;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(packUri, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    result = bmp;
                }
                catch
                {
                    // pack 失败：回退到应用基目录
                    string path = Path.Combine(System.AppContext.BaseDirectory, ResourceDir, fileName);
                    if (File.Exists(path))
                    {
                        var bmp = new BitmapImage();
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = fs;
                            bmp.EndInit();
                        }
                        bmp.Freeze();
                        result = bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("加载版本图标失败 [" + fileName + "]: " + ex.Message);
                result = null;
            }

            // 仍然失败，兜底用一个简单的彩色方块
            if (result == null)
            {
                try
                {
                    result = CreateFallback();
                }
                catch
                {
                    result = null;
                }
            }

            lock (_lockObj)
            {
                _iconCache[fileName] = result;
            }

            return result;
        }

        private static ImageSource CreateFallback()
        {
            // 简单绘制一个 32x32 的灰棕色方块作为兜底
            var drawingGroup = new DrawingGroup();
            const int pixelSize = 2;
            const int grid = 16;
            var rand = new Random(0x4D43);
            for (int y = 0; y < grid; y++)
            {
                for (int x = 0; x < grid; x++)
                {
                    int r = 90 + rand.Next(40);
                    int g = 60 + rand.Next(30);
                    int b = 40 + rand.Next(25);
                    var brush = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
                    brush.Freeze();
                    drawingGroup.Children.Add(new GeometryDrawing(brush, null,
                        new RectangleGeometry(new Rect(x * pixelSize, y * pixelSize, pixelSize, pixelSize))));
                }
            }
            var drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            return drawingImage;
        }
    }
}
