using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 以编程方式生成正确的 Steve/Alex 默认皮肤（64x64, BGRA32, 所有区域都填充颜色，没有透明像素）。
    /// 这避免了从 PNG 文件加载时的透明像素 → 白色块的问题。
    /// </summary>
    public static class DefaultSkinFactory
    {
        private const int SIZE = 64;

        private struct Rgb
        {
            public byte R, G, B;
            public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }
        }

        private struct Palette
        {
            public Rgb Skin;       // 肤色
            public Rgb SkinDark;   // 肤色深色阴影
            public Rgb Shirt;      // 上衣颜色
            public Rgb ShirtDark;  // 上衣深色阴影/夹克
            public Rgb Pants;      // 裤子颜色
            public Rgb PantsDark;  // 裤子深色
            public Rgb Hair;       // 头发颜色
            public Rgb Eye;        // 眼睛颜色
        }

        private static Palette StevePalette = new Palette
        {
            Skin = new Rgb(193, 135, 79),
            SkinDark = new Rgb(160, 100, 60),
            Shirt = new Rgb(50, 110, 180),    // 青色衬衫
            ShirtDark = new Rgb(30, 80, 140),
            Pants = new Rgb(50, 50, 120),       // 深蓝裤子
            PantsDark = new Rgb(30, 30, 80),
            Hair = new Rgb(58, 35, 15),          // 深棕头发
            Eye = new Rgb(30, 30, 30)
        };

        private static Palette AlexPalette = new Palette
        {
            Skin = new Rgb(230, 180, 150),
            SkinDark = new Rgb(195, 140, 110),
            Shirt = new Rgb(60, 110, 80),       // 绿色衬衫
            ShirtDark = new Rgb(40, 80, 60),
            Pants = new Rgb(90, 60, 130),        // 紫色裤子
            PantsDark = new Rgb(60, 40, 90),
            Hair = new Rgb(180, 70, 50),           // 红棕头发
            Eye = new Rgb(30, 30, 30)
        };

        public static BitmapSource CreateSteve()
        {
            return BuildBitmap(StevePalette, isAlex: false);
        }

        public static BitmapSource CreateAlex()
        {
            return BuildBitmap(AlexPalette, isAlex: true);
        }

        /// <summary>
        /// 统一的默认皮肤加载入口：优先从 Assets/Skins/steve.png 或 alex.png 加载（真实的 Minecraft 默认皮肤），
        /// 加载失败时回退到代码生成的简化皮肤。所有需要默认皮肤的地方（2D 立雕、3D 预览、账号列表、主页面头像）
        /// 都应当调用本方法，以保证展示效果一致。
        /// </summary>
        public static BitmapSource GetDefaultSkinBitmap(bool isSlim)
        {
            string name = isSlim ? "alex.png" : "steve.png";
            string exeDir = System.AppContext.BaseDirectory;
            string filePath = Path.Combine(exeDir, "Assets", "Skins", name);
            try
            {
                if (File.Exists(filePath))
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var formatted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                        formatted.Freeze();
                        return formatted;
                    }
                }
            }
            catch { }

            // 兜底：代码生成的简化皮肤
            return isSlim ? CreateAlex() : CreateSteve();
        }

        private static WriteableBitmap BuildBitmap(Palette pal, bool isAlex)
        {
            int stride = SIZE * 4;
            byte[] pixels = new byte[SIZE * stride];

            // === 1. 头部内层 (0,0)-(32,16) —— 填充皮肤色作为默认
            FillRect(pixels, SIZE, 0, 0, 32, 16, pal.Skin, pal.SkinDark);

            // === 2. 头部外层（帽子/头发）(32,0)-(64,16) —— 填充头发色
            FillRect(pixels, SIZE, 32, 0, 32, 16, pal.Hair, pal.Hair);

            // === 3. 身体内层 (16,16)-(40,32) —— 衬衫色
            FillRect(pixels, SIZE, 16, 16, 24, 16, pal.Shirt, pal.ShirtDark);

            // === 4. 身体外层（夹克） (16,32)-(40,48)
            FillRect(pixels, SIZE, 16, 32, 24, 16, pal.ShirtDark, pal.ShirtDark);

            // === 5. 右臂内层 (40,16)-(56,32)
            FillRect(pixels, SIZE, 40, 16, 16, 16, pal.Shirt, pal.ShirtDark);

            // === 6. 左臂内层 (32,48)-(48,64)
            FillRect(pixels, SIZE, 32, 48, 16, 16, pal.Shirt, pal.ShirtDark);

            // === 7. 右臂外层（袖） (40,32)-(56,48)
            FillRect(pixels, SIZE, 40, 32, 16, 16, pal.ShirtDark, pal.ShirtDark);

            // === 8. 左臂外层 (48,48)-(64,64)
            FillRect(pixels, SIZE, 48, 48, 16, 16, pal.ShirtDark, pal.ShirtDark);

            // === 9. 右腿内层 (0,16)-(16,32)
            FillRect(pixels, SIZE, 0, 16, 16, 16, pal.Pants, pal.PantsDark);

            // === 10. 左腿内层 (16,48)-(32,64)
            FillRect(pixels, SIZE, 16, 48, 16, 16, pal.Pants, pal.PantsDark);

            // === 11. 右腿外层（裤） (0,32)-(16,48)
            FillRect(pixels, SIZE, 0, 32, 16, 16, pal.PantsDark, pal.PantsDark);

            // === 12. 左腿外层 (0,48)-(16,64)
            FillRect(pixels, SIZE, 0, 48, 16, 16, pal.PantsDark, pal.PantsDark);

            // === 13. 面部细节 —— 在头部内层的"前面"区域 (x=8..15, y=8..15) 画眼睛
            // 左眼（x=16+2..16+4, y=8+3..8+5）—— 实际内层头前面区域: Minecraft 标准布局中前面是 x=8..15,y=8..15
            DrawEyes(pixels, SIZE, pal);

            var wb = new WriteableBitmap(SIZE, SIZE, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, SIZE, SIZE), pixels, stride, 0);
            wb.Freeze();
            return wb;
        }

        private static void FillRect(byte[] pixels, int stride, int x, int y, int w, int h, Rgb color, Rgb dark)
        {
            for (int yy = 0; yy < h; yy++)
            {
                for (int xx = 0; xx < w; xx++)
                {
                    int idx = ((y + yy) * stride + (x + xx)) * 4;
                    // 简单规则：让边界像素略深，产生边缘阴影感
                    bool isEdge = (xx == 0 || xx == w - 1 || yy == 0 || yy == h - 1);
                    Rgb c = isEdge ? dark : color;
                    pixels[idx + 0] = c.B;
                    pixels[idx + 1] = c.G;
                    pixels[idx + 2] = c.R;
                    pixels[idx + 3] = 255; // 完全不透明
                }
            }
        }

        private static void DrawEyes(byte[] pixels, int stride, Palette pal)
        {
            // 在头部前面 (x=8..15, y=8..15) 画两只眼睛
            // 左眼: (10,10)-(12,12), 右眼: (13,10)-(15,12)
            SetPixel(pixels, SIZE, 10, 10, pal.Eye);
            SetPixel(pixels, SIZE, 11, 10, pal.Eye);
            SetPixel(pixels, SIZE, 12, 10, pal.Eye);
            SetPixel(pixels, SIZE, 10, 11, pal.Eye);
            SetPixel(pixels, SIZE, 11, 11, new Rgb(240, 240, 240)); // 眼白
            SetPixel(pixels, SIZE, 12, 11, pal.Eye);
            SetPixel(pixels, SIZE, 10, 12, pal.Eye);
            SetPixel(pixels, SIZE, 11, 12, pal.Eye);
            SetPixel(pixels, SIZE, 12, 12, pal.Eye);

            SetPixel(pixels, SIZE, 13, 10, pal.Eye);
            SetPixel(pixels, SIZE, 14, 10, pal.Eye);
            SetPixel(pixels, SIZE, 15, 10, pal.Eye);
            SetPixel(pixels, SIZE, 13, 11, pal.Eye);
            SetPixel(pixels, SIZE, 14, 11, new Rgb(240, 240, 240));
            SetPixel(pixels, SIZE, 15, 11, pal.Eye);
            SetPixel(pixels, SIZE, 13, 12, pal.Eye);
            SetPixel(pixels, SIZE, 14, 12, pal.Eye);
            SetPixel(pixels, SIZE, 15, 12, pal.Eye);
        }

        private static void SetPixel(byte[] pixels, int stride, int x, int y, Rgb c)
        {
            int idx = (y * stride + x) * 4;
            pixels[idx + 0] = c.B;
            pixels[idx + 1] = c.G;
            pixels[idx + 2] = c.R;
            pixels[idx + 3] = 255;
        }

        /// <summary>
        /// 将 BitmapSource 保存到 PNG 文件（用于刷新 Assets/Skins/ 下的默认皮肤文件）
        /// </summary>
        public static void SavePng(BitmapSource bmp, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(fs);
        }
    }
}
