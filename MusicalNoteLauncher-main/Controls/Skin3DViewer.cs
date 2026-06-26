using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MusicalNoteLauncher.Controls
{
    public class Skin3DViewer : Grid
    {
        private Viewport3D _viewport;
        private PerspectiveCamera _camera;
        private Model3DGroup _modelGroup;
        private ModelVisual3D _modelVisual;
        private AmbientLight _ambientLight;

        private AxisAngleRotation3D _yawRotation;
        private AxisAngleRotation3D _pitchRotation;
        private Point _lastMousePos;
        private bool _isDragging;
        private double _zoomScale = 1.0;

        private BitmapSource _skinBitmap;
        private bool _isSlim;

        public Skin3DViewer()
        {
            InitializeViewport();
        }

        private void InitializeViewport()
        {
            _camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 0, 80),
                LookDirection = new Vector3D(0, 0, -1),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45
            };

            var cameraGroup = new Transform3DGroup();
            _yawRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            _pitchRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            cameraGroup.Children.Add(new RotateTransform3D(_pitchRotation));
            cameraGroup.Children.Add(new RotateTransform3D(_yawRotation));
            _camera.Transform = cameraGroup;

            _ambientLight = new AmbientLight(Color.FromRgb(255, 255, 255));

            _modelGroup = new Model3DGroup();
            _modelGroup.Children.Add(_ambientLight);

            _modelVisual = new ModelVisual3D { Content = _modelGroup };

            _viewport = new Viewport3D
            {
                Camera = _camera,
                ClipToBounds = true
            };
            _viewport.Children.Add(_modelVisual);

            _viewport.MouseDown += OnMouseDown;
            _viewport.MouseMove += OnMouseMove;
            _viewport.MouseUp += OnMouseUp;
            _viewport.MouseWheel += OnMouseWheel;

            Unloaded += OnUnloaded;

            Children.Add(_viewport);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ClearModel();
            _skinBitmap = null;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePos = e.GetPosition(_viewport);
                _viewport.CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var pos = e.GetPosition(_viewport);
            double dx = pos.X - _lastMousePos.X;
            double dy = pos.Y - _lastMousePos.Y;

            double sensitivity = 0.5;
            _yawRotation.Angle += dx * sensitivity;
            _pitchRotation.Angle = Math.Max(-80, Math.Min(80, _pitchRotation.Angle + dy * sensitivity));

            _lastMousePos = pos;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _viewport.ReleaseMouseCapture();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 0.9 : 1.1;
            _zoomScale *= factor;
            _zoomScale = Math.Max(0.3, Math.Min(3.0, _zoomScale));
            _camera.Position = new Point3D(_camera.Position.X, _camera.Position.Y, 80.0 * _zoomScale);
        }

        public void UpdateSkin(BitmapSource skinImage, bool isSlim)
        {
            _isSlim = isSlim;

            if (skinImage == null)
            {
                ClearModel();
                _skinBitmap = null;
                return;
            }

            BitmapSource processed = skinImage;
            if (processed.PixelHeight * 2 == processed.PixelWidth)
            {
                processed = ConvertX32ToX64(processed);
            }

            int multiple = Math.Max((int)(1024.0 / processed.PixelWidth), 1);
            if (multiple > 1)
            {
                processed = EnlargeImage(processed, multiple, multiple);
            }

            _skinBitmap = processed;
            RebuildModel();
        }

        private void ClearModel()
        {
            var toRemove = new List<Model3D>();
            foreach (var child in _modelGroup.Children)
                if (child is GeometryModel3D || child is Model3DGroup)
                    toRemove.Add(child);
            foreach (var child in toRemove)
                _modelGroup.Children.Remove(child);
        }

        private static BitmapSource ConvertX32ToX64(BitmapSource src)
        {
            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;
            int dstW = srcW;
            int dstH = srcW;

            var srcPixels = new byte[srcW * srcH * 4];
            src.CopyPixels(srcPixels, srcW * 4, 0);

            var dstPixels = new byte[dstW * dstH * 4];

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < dstW; x++)
                    for (int c = 0; c < 4; c++)
                        dstPixels[(y * dstW + x) * 4 + c] = srcPixels[(y * srcW + x) * 4 + c];

            for (int y = 16; y < srcH; y++)
                for (int x = 0; x < 16; x++)
                    for (int c = 0; c < 4; c++)
                        dstPixels[(y * dstW + x) * 4 + c] = srcPixels[(y * srcW + x) * 4 + c];

            for (int y = 16; y < srcH; y++)
                for (int x = 16; x < 40; x++)
                    for (int c = 0; c < 4; c++)
                        dstPixels[(y * dstW + x) * 4 + c] = srcPixels[(y * srcW + x) * 4 + c];

            for (int y = 16; y < srcH; y++)
                for (int x = 40; x < dstW; x++)
                    for (int c = 0; c < 4; c++)
                        dstPixels[(y * dstW + x) * 4 + c] = srcPixels[(y * srcW + x) * 4 + c];

            ExpandPart(srcPixels, dstPixels, srcW, srcH, dstW,
                srcSkinX: 0, srcSkinY: 16, dstSkinX: 16, dstSkinY: 48,
                partW: 4, partH: 12, partD: 4);

            ExpandPart(srcPixels, dstPixels, srcW, srcH, dstW,
                srcSkinX: 40, srcSkinY: 16, dstSkinX: 32, dstSkinY: 48,
                partW: 4, partH: 12, partD: 4);

            var result = BitmapSource.Create(dstW, dstH, 96, 96, PixelFormats.Bgra32, null, dstPixels, dstW * 4);
            result.Freeze();
            return result;
        }

        private static void ExpandPart(byte[] src, byte[] dst, int srcW, int srcH, int dstW,
            int srcSkinX, int srcSkinY, int dstSkinX, int dstSkinY,
            int partW, int partH, int partD)
        {
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX + partD, srcSkinY, dstSkinX + partD, dstSkinY,
                partW, partD, true, false);
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX + partD + partW, srcSkinY, dstSkinX + partD + partW, dstSkinY,
                partW, partD, true, false);
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX, srcSkinY + partD * 2, dstSkinX + partW + partD, dstSkinY + partD,
                partD, partH, true, false);
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX + partW + partD, srcSkinY + partD * 2, dstSkinX, dstSkinY + partD,
                partD, partH, true, false);
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX + partD, srcSkinY + partD * 2, dstSkinX + partD, dstSkinY + partD,
                partW, partH, true, false);
            CopyRegion(src, dst, srcW, srcH, dstW,
                srcSkinX + partD * 2 + partW, srcSkinY + partD * 2, dstSkinX + partD * 2 + partW, dstSkinY + partD,
                partW, partH, true, false);
        }

        private static void CopyRegion(byte[] src, byte[] dst, int srcW, int srcH, int dstW,
            int srcX, int srcY, int dstX, int dstY, int w, int h, bool revX, bool revY)
        {
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int sx = srcX + (revX ? w - x - 1 : x);
                    int sy = srcY + (revY ? h - y - 1 : y);
                    int dx = dstX + x;
                    int dy = dstY + y;

                    if (sx < 0 || sx >= srcW || sy < 0 || sy >= srcH) continue;
                    if (dx < 0 || dx >= dstW || dy < 0 || dy >= dstW) continue;

                    int srcIdx = (sy * srcW + sx) * 4;
                    int dstIdx = (dy * dstW + dx) * 4;

                    for (int c = 0; c < 4; c++)
                        dst[dstIdx + c] = src[srcIdx + c];
                }
        }

        private static BitmapSource EnlargeImage(BitmapSource src, int mulX, int mulY)
        {
            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;
            int dstW = srcW * mulX;
            int dstH = srcH * mulY;

            var srcPixels = new byte[srcW * srcH * 4];
            src.CopyPixels(srcPixels, srcW * 4, 0);

            var dstPixels = new byte[dstW * dstH * 4];
            for (int x = 0; x < srcW; x++)
                for (int y = 0; y < srcH; y++)
                    for (int mx = 0; mx < mulX; mx++)
                        for (int my = 0; my < mulY; my++)
                        {
                            int srcIdx = (y * srcW + x) * 4;
                            int dstIdx = ((y * mulY + my) * dstW + (x * mulX + mx)) * 4;
                            for (int c = 0; c < 4; c++)
                                dstPixels[dstIdx + c] = srcPixels[srcIdx + c];
                        }

            var result = BitmapSource.Create(dstW, dstH, 96, 96, PixelFormats.Bgra32, null, dstPixels, dstW * 4);
            result.Freeze();
            return result;
        }

        private void RebuildModel()
        {
            ClearModel();
            if (_skinBitmap == null) return;

            double armW = _isSlim ? 3.0 : 4.0;
            double armScaleX = _isSlim ? 14.0 / 64.0 : 16.0 / 64.0;

            // 内层
            _modelGroup.Children.Add(CreatePart(
                width: 8, height: 8, depth: 8,
                startX: 0.0 / 64.0, startY: 0.0 / 64.0,
                scaleX: 32.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(0, +(12.0 + 8.0) / 2.0, 0),
                enlarge: 0.0));

            _modelGroup.Children.Add(CreatePart(
                width: 8, height: 12, depth: 4,
                startX: 16.0 / 64.0, startY: 16.0 / 64.0,
                scaleX: 24.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(0, 0, 0),
                enlarge: 0.0));

            _modelGroup.Children.Add(CreatePart(
                width: armW, height: 12, depth: 4,
                startX: 40.0 / 64.0, startY: 16.0 / 64.0,
                scaleX: armScaleX, scaleY: 16.0 / 64.0,
                offset: new Point3D(+(8.0 + armW) / 2.0, 0, 0),
                enlarge: 0.0));

            _modelGroup.Children.Add(CreatePart(
                width: armW, height: 12, depth: 4,
                startX: 32.0 / 64.0, startY: 48.0 / 64.0,
                scaleX: armScaleX, scaleY: 16.0 / 64.0,
                offset: new Point3D(-(8.0 + armW) / 2.0, 0, 0),
                enlarge: 0.0));

            _modelGroup.Children.Add(CreatePart(
                width: 4, height: 12, depth: 4,
                startX: 0.0 / 64.0, startY: 16.0 / 64.0,
                scaleX: 16.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(+(8.0 - 4.0) / 2.0, -(12.0 + 12.0) / 2.0, 0),
                enlarge: 0.0));

            _modelGroup.Children.Add(CreatePart(
                width: 4, height: 12, depth: 4,
                startX: 16.0 / 64.0, startY: 48.0 / 64.0,
                scaleX: 16.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(-(8.0 - 4.0) / 2.0, -(12.0 + 12.0) / 2.0, 0),
                enlarge: 0.0));

            // 外层（略大尺寸 + 外层UV）
            _modelGroup.Children.Add(CreatePart(
                width: 8, height: 8, depth: 8,
                startX: 32.0 / 64.0, startY: 0.0 / 64.0,
                scaleX: 32.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(0, +(12.0 + 8.0) / 2.0, 0),
                enlarge: 0.25));

            _modelGroup.Children.Add(CreatePart(
                width: 8, height: 12, depth: 4,
                startX: 16.0 / 64.0, startY: 32.0 / 64.0,
                scaleX: 24.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(0, 0, 0),
                enlarge: 0.2));

            _modelGroup.Children.Add(CreatePart(
                width: armW, height: 12, depth: 4,
                startX: 40.0 / 64.0, startY: 32.0 / 64.0,
                scaleX: armScaleX, scaleY: 16.0 / 64.0,
                offset: new Point3D(+(8.0 + armW) / 2.0, 0, 0),
                enlarge: 0.2));

            _modelGroup.Children.Add(CreatePart(
                width: armW, height: 12, depth: 4,
                startX: 48.0 / 64.0, startY: 48.0 / 64.0,
                scaleX: armScaleX, scaleY: 16.0 / 64.0,
                offset: new Point3D(-(8.0 + armW) / 2.0, 0, 0),
                enlarge: 0.2));

            _modelGroup.Children.Add(CreatePart(
                width: 4, height: 12, depth: 4,
                startX: 0.0 / 64.0, startY: 32.0 / 64.0,
                scaleX: 16.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(+(8.0 - 4.0) / 2.0, -(12.0 + 12.0) / 2.0, 0),
                enlarge: 0.2));

            _modelGroup.Children.Add(CreatePart(
                width: 4, height: 12, depth: 4,
                startX: 0.0 / 64.0, startY: 48.0 / 64.0,
                scaleX: 16.0 / 64.0, scaleY: 16.0 / 64.0,
                offset: new Point3D(-(8.0 - 4.0) / 2.0, -(12.0 + 12.0) / 2.0, 0),
                enlarge: 0.2));
        }

        private Model3D CreatePart(double width, double height, double depth,
            double startX, double startY, double scaleX, double scaleY,
            Point3D offset, double enlarge)
        {
            var mesh = BuildCubeMeshHMCL(width + enlarge, height + enlarge, depth + enlarge,
                startX, startY, scaleX, scaleY);
            var imageBrush = new ImageBrush(_skinBitmap);
            imageBrush.Stretch = Stretch.Fill;
            imageBrush.ViewportUnits = BrushMappingMode.Absolute;
            // WPF DiffuseMaterial 尊重图像 alpha，外层皮肤空白区保持透明 -> 内层透出
            var material = new DiffuseMaterial(imageBrush);
            var model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;
            model.Transform = new TranslateTransform3D(offset.X, offset.Y, offset.Z);
            return model;
        }

        // 严格复刻 HMCL SkinCube.Model（Java）。仅两项改动：
        //   1) 顶点 Y 取反（HMCL Y 向下，WPF Y 向上）
        //   2) 每三角形顶点顺序反转（保持面朝观察者）
        // UV 不翻转 - WPF ImageBrush 把 v=0 视作图像顶部，与 HMCL 一致。
        private MeshGeometry3D BuildCubeMeshHMCL(double w, double h, double d,
            double startX, double startY, double scaleX, double scaleY)
        {
            double hw = w / 2.0, hh = h / 2.0, hd = d / 2.0;

            // P0..P7：HMCL 原始坐标，Y 值取反
            var positions = new Point3D[]
            {
                new Point3D(-hw, +hh, +hd),
                new Point3D(+hw, +hh, +hd),
                new Point3D(-hw, -hh, +hd),
                new Point3D(+hw, -hh, +hd),
                new Point3D(-hw, +hh, -hd),
                new Point3D(+hw, +hh, -hd),
                new Point3D(-hw, -hh, -hd),
                new Point3D(+hw, -hh, -hd),
            };

            double totalX = (w + d) * 2.0;
            double totalY = h + d;
            double half_width = w / totalX * scaleX;
            double half_depth = d / totalX * scaleX;
            double top_x = d / totalX * scaleX + startX;
            double bottom_x = startX;
            double top_y = startY;
            double middle_y = d / totalY * scaleY + top_y;
            double bottom_y = scaleY + top_y;

            // T0..T12：与 HMCL 完全一致
            var uvPoints = new Point[]
            {
                new Point(top_x,                                top_y),
                new Point(top_x + half_width,                   top_y),
                new Point(top_x + half_width * 2,               top_y),
                new Point(bottom_x,                             middle_y),
                new Point(bottom_x + half_depth,                middle_y),
                new Point(bottom_x + half_depth + half_width,   middle_y),
                new Point(bottom_x + scaleX - half_width,       middle_y),
                new Point(bottom_x + scaleX,                    middle_y),
                new Point(bottom_x,                             bottom_y),
                new Point(bottom_x + half_depth,                bottom_y),
                new Point(bottom_x + half_depth + half_width,   bottom_y),
                new Point(bottom_x + scaleX - half_width,       bottom_y),
                new Point(bottom_x + scaleX,                    bottom_y),
            };

            // 复制自 HMCL createFaces()
            int[][] hmclFaces = new int[][]
            {
                new int[] {5, 0, 4, 1, 0, 5},
                new int[] {5, 0, 0, 5, 1, 4},
                new int[] {5, 3, 1, 4, 3, 9},
                new int[] {5, 3, 3, 9, 7, 8},
                new int[] {1, 4, 0, 5, 2, 10},
                new int[] {1, 4, 2, 10, 3, 9},
                new int[] {0, 5, 4, 6, 6, 11},
                new int[] {0, 5, 6, 11, 2, 10},
                new int[] {4, 6, 5, 7, 7, 12},
                new int[] {4, 6, 7, 12, 6, 11},
                new int[] {3, 5, 2, 6, 6, 2},
                new int[] {3, 5, 6, 2, 7, 1},
            };

            var mesh = new MeshGeometry3D();
            int triIndex = 0;
            foreach (var face in hmclFaces)
            {
                // 反转 3 个顶点顺序，以补偿 Y 取反带来的朝向变化
                for (int k = 2; k >= 0; k--)
                {
                    int pIdx = face[k * 2];
                    int tIdx = face[k * 2 + 1];
                    mesh.Positions.Add(positions[pIdx]);
                    mesh.TextureCoordinates.Add(uvPoints[tIdx]);
                    mesh.TriangleIndices.Add(triIndex++);
                }
            }

            return mesh;
        }
    }

    /// <summary>
    /// 生成默认 Steve/Alex 皮肤。严格按照 Minecraft 64×64 皮肤格式布局，
    /// 各部位填充清晰的像素色块，确保头部正面 (x=8..15, y=8..15) 能正确识别为玩家脸部，
    /// 并保留肤色 + 脸部特征 + 头发颜色等细节。
    /// </summary>
    public static class DefaultSkinGenerator
    {
        // 通用颜色（BGRA 字节顺序：tuple 字段 b→R通道, g→G通道, r→B通道）
        // 重要：SetPixel(..., c.r, c.g, c.b, c.a) 会把 c.r 写入像素的 R 通道，依此类推。
        // 所以要得到显示色 RGB(R, G, B)，需写为 (b: B, g: G, r: R, a: A)。
        private static readonly (byte b, byte g, byte r, byte a) SKIN_BASE = (b: 125, g: 194, r: 241, a: 255);     // 桃色肤色
        private static readonly (byte b, byte g, byte r, byte a) SKIN_SHADOW = (b: 98, g: 148, r: 178, a: 255);     // 肤色阴影
        private static readonly (byte b, byte g, byte r, byte a) SKIN_LIP = (b: 70, g: 110, r: 150, a: 255);        // 唇色

        // Steve 头发：深棕
        private static readonly (byte b, byte g, byte r, byte a) HAIR_STEVE = (b: 17, g: 33, r: 59, a: 255);
        private static readonly (byte b, byte g, byte r, byte a) HAIR_STEVE_SHADOW = (b: 10, g: 20, r: 35, a: 255);

        // Alex 头发：橙红
        private static readonly (byte b, byte g, byte r, byte a) HAIR_ALEX = (b: 30, g: 140, r: 255, a: 255);
        private static readonly (byte b, byte g, byte r, byte a) HAIR_ALEX_SHADOW = (b: 20, g: 100, r: 200, a: 255);

        // 衣服/裤子/鞋子
        private static readonly (byte b, byte g, byte r, byte a) SHIRT_BASE = (b: 170, g: 170, r: 0, a: 255);       // 青色衬衫 (Steve)
        private static readonly (byte b, byte g, byte r, byte a) SHIRT_SHADOW = (b: 120, g: 120, r: 0, a: 255);
        private static readonly (byte b, byte g, byte r, byte a) PANTS_BASE = (b: 150, g: 60, r: 60, a: 255);        // 蓝紫裤
        private static readonly (byte b, byte g, byte r, byte a) PANTS_SHADOW = (b: 110, g: 40, r: 40, a: 255);
        private static readonly (byte b, byte g, byte r, byte a) SHOE = (b: 50, g: 50, r: 50, a: 255);

        /// <summary>生成默认 Steve 皮肤（64×64，32bpp BGRA）</summary>
        public static BitmapSource CreateSteveSkin()
        {
            const int size = 64;
            var pixels = new byte[size * size * 4];

            // 默认全部透明（未使用区域）
            for (int i = 0; i < pixels.Length; i += 4)
                pixels[i + 3] = 0;

            // ========== 头部区域 (y=0..15) ==========
            // 内层头部：整个 8×8 × 6 个面，但我们只关心正面 (x=8..15, y=8..15)
            // 正面：玩家脸部（肤色 + 眼睛 + 嘴）
            // 8×8 脸部布局：
            //   y=0 (顶部):  头发阴影
            //   y=1..2:     头发
            //   y=3:        眼睛区域
            //   y=4..5:     鼻子/脸颊
            //   y=6..7:     嘴/下巴
            FillHead(pixels, size, useSteve: true);

            // ========== 身体区域 (y=16..31) ==========
            // 内层身体：x=16..39, y=16..31（宽24×高16）
            FillBody(pixels, size);

            // 外层身体：x=16..39, y=32..47（与内层重叠但略大）
            FillBodyOverlay(pixels, size);

            // ========== 右臂区域 (y=16..31, x=40..55) ==========
            // 右臂内层：x=40..55, y=16..31（宽16×高16）
            FillArmRight(pixels, size, width: 16);
            // 右臂外层：x=40..55, y=32..47
            FillArmRightOverlay(pixels, size, width: 16);

            // ========== 右腿区域 (y=16..31, x=0..15) ==========
            // 右腿内层：x=0..15, y=16..31
            FillLegRight(pixels, size);
            // 右腿外层：x=0..15, y=32..47
            FillLegRightOverlay(pixels, size);

            // ========== 左臂区域 (y=48..63) ==========
            // 左臂内层：x=32..47, y=48..63
            FillArmLeft(pixels, size, width: 16);
            // 左臂外层：x=48..63, y=48..63
            FillArmLeftOverlay(pixels, size, width: 16);

            // ========== 左腿区域 (y=48..63) ==========
            // 左腿内层：x=16..31, y=48..63
            FillLegLeft(pixels, size);
            // 左腿外层：x=0..15, y=48..63
            FillLegLeftOverlay(pixels, size);

            var wb = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            wb.Freeze();
            return wb;
        }

        /// <summary>生成默认 Alex 皮肤（头发颜色不同，手臂宽度 14）</summary>
        public static BitmapSource CreateAlexSkin()
        {
            const int size = 64;
            var pixels = new byte[size * size * 4];

            for (int i = 0; i < pixels.Length; i += 4)
                pixels[i + 3] = 0;

            // 与 Steve 相同，但头发颜色不同、手臂宽度 14
            FillHead(pixels, size, useSteve: false);
            FillBody(pixels, size);
            FillBodyOverlay(pixels, size);
            FillArmRight(pixels, size, width: 14);
            FillArmRightOverlay(pixels, size, width: 14);
            FillLegRight(pixels, size);
            FillLegRightOverlay(pixels, size);
            FillArmLeft(pixels, size, width: 14);
            FillArmLeftOverlay(pixels, size, width: 14);
            FillLegLeft(pixels, size);
            FillLegLeftOverlay(pixels, size);

            var wb = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            wb.Freeze();
            return wb;
        }

        // ============== 头部绘制 ==============
        // 内层头部正面：x=8..15, y=8..15（8×8 玩家脸部）
        // 外层头部正面：x=40..47, y=8..15（8×8 头发，透明处透出内层）
        // 其他面（顶、底、左、右、后）也填充颜色，便于 3D 渲染
        private static void FillHead(byte[] pixels, int size, bool useSteve)
        {
            var hair = useSteve ? HAIR_STEVE : HAIR_ALEX;
            var hairShadow = useSteve ? HAIR_STEVE_SHADOW : HAIR_ALEX_SHADOW;

            // ---- 内层头部正面 (8..15, 8..15)：完整脸部 ----
            // 8×8 像素布局（dy 从上到下 0..7，dx 从左到右 0..7）：
            //   dy=0: S S S S S S S S   （额头，肤色）
            //   dy=1: S S S S S S S S   （额头）
            //   dy=2: S H S S S S H S   （眉毛）
            //   dy=3: S W E S S W E S   （眼睛：W=眼白，E=眼珠）
            //   dy=4: S S S S S S S S   （脸颊）
            //   dy=5: S S S N N S S S   （鼻子阴影）
            //   dy=6: S S S M M S S S   （嘴部）
            //   dy=7: S S S S S S S S   （下巴）
            var eyeWhite = (b: (byte)240, g: (byte)240, r: (byte)240, a: (byte)255);
            var eyeBall = useSteve
                ? (b: (byte)30, g: (byte)70, r: (byte)120, a: (byte)255)     // Steve 棕眼睛 RGB(120,70,30)
                : (b: (byte)80, g: (byte)180, r: (byte)80, a: (byte)255);    // Alex 绿眼睛 RGB(80,180,80)
            var brow = hairShadow;                    // 眉毛用头发深色
            var nose = (b: (byte)70, g: (byte)110, r: (byte)150, a: (byte)255);    // RGB(150,110,70)
            var mouth = (b: (byte)50, g: (byte)70, r: (byte)100, a: (byte)255);    // RGB(100,70,50)

            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    var c = SKIN_BASE;

                    if (dy == 2 && (dx == 1 || dx == 6)) c = brow;             // 眉毛
                    else if (dy == 3)
                    {
                        if (dx == 1 || dx == 5) c = eyeWhite;                    // 眼白
                        else if (dx == 2 || dx == 6) c = eyeBall;                // 眼珠
                    }
                    else if (dy == 5 && (dx == 3 || dx == 4)) c = nose;          // 鼻子阴影
                    else if (dy == 6 && (dx == 3 || dx == 4)) c = mouth;         // 嘴

                    SetPixel(pixels, size, x: 8 + dx, y: 8 + dy, c.r, c.g, c.b, c.a);
                }

            // 内层顶部 (8..15, 0..7)：头顶
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    var c = dy < 4 ? hair : hairShadow;
                    SetPixel(pixels, size, x: 8 + dx, y: 0 + dy, c.r, c.g, c.b, c.a);
                }

            // 内层左/右侧面 (0..7, 8..15) 和 (16..23, 8..15)
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    var c = dx == 0 ? SKIN_SHADOW : SKIN_BASE;
                    SetPixel(pixels, size, x: 0 + dx, y: 8 + dy, c.r, c.g, c.b, c.a);
                    SetPixel(pixels, size, x: 16 + dx, y: 8 + dy, c.r, c.g, c.b, c.a);
                }

            // 内层后面 (24..31, 8..15)：后脑勺头发
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    var c = dy < 4 ? hair : hairShadow;
                    SetPixel(pixels, size, x: 24 + dx, y: 8 + dy, c.r, c.g, c.b, c.a);
                }

            // ---- 外层头部：头发/帽子 ----
            // 外层顶部 (40..47, 0..7)：完整填充
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    var cTop = dy < 4 ? hair : hairShadow;
                    SetPixel(pixels, size, x: 40 + dx, y: 0 + dy, cTop.r, cTop.g, cTop.b, cTop.a);
                }

            // 外层正面 (40..47, 8..15)：仅顶部 dy=0 一行为头发刘海，其余透明
            // 这样内层的眉毛、眼睛、鼻子、嘴可以完整显示
            for (int dx = 0; dx < 8; dx++)
            {
                SetPixel(pixels, size, x: 40 + dx, y: 8 + 0, hair.r, hair.g, hair.b, hair.a);
                // dy=1..7 保持透明
            }

            // 外层左右面 (32..39, 8..15) 和 (48..55, 8..15)
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    SetPixel(pixels, size, x: 32 + dx, y: 8 + dy, hair.r, hair.g, hair.b, hair.a);
                    SetPixel(pixels, size, x: 48 + dx, y: 8 + dy, hair.r, hair.g, hair.b, hair.a);
                }

            // 外层后面 (56..63, 8..15)
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 8; dx++)
                {
                    SetPixel(pixels, size, x: 56 + dx, y: 8 + dy, hairShadow.r, hairShadow.g, hairShadow.b, hairShadow.a);
                }
        }

        // ============== 身体绘制 ==============
        // 内层身体：x=16..39 (w=24), y=16..31 (h=16)
        private static void FillBody(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 24; dx++)
                {
                    var c = (dx >= 8 && dx <= 15) ? SHIRT_BASE : SHIRT_SHADOW; // 中间略亮
                    SetPixel(pixels, size, x: 16 + dx, y: 16 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // 外层身体：x=16..39, y=32..47
        private static void FillBodyOverlay(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 24; dx++)
                {
                    var c = (dx + dy) % 3 == 0 ? SHIRT_SHADOW : SHIRT_BASE;
                    SetPixel(pixels, size, x: 16 + dx, y: 32 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // ============== 手臂绘制 ==============
        private static void FillArmRight(byte[] pixels, int size, int width)
        {
            // 右臂内层：x=40..40+width-1, y=16..31
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    var c = (dy < 4) ? SKIN_BASE : SHIRT_BASE; // 上方皮肤，下方衣服
                    SetPixel(pixels, size, x: 40 + dx, y: 16 + dy, c.r, c.g, c.b, c.a);
                }
        }

        private static void FillArmRightOverlay(byte[] pixels, int size, int width)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    var c = (dx + dy) % 4 == 0 ? SHIRT_SHADOW : SHIRT_BASE;
                    SetPixel(pixels, size, x: 40 + dx, y: 32 + dy, c.r, c.g, c.b, c.a);
                }
        }

        private static void FillArmLeft(byte[] pixels, int size, int width)
        {
            // 左臂内层：x=32..31+width, y=48..63
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    var c = (dy < 4) ? SKIN_BASE : SHIRT_BASE;
                    SetPixel(pixels, size, x: 32 + dx, y: 48 + dy, c.r, c.g, c.b, c.a);
                }
        }

        private static void FillArmLeftOverlay(byte[] pixels, int size, int width)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    var c = (dx + dy) % 4 == 0 ? SHIRT_SHADOW : SHIRT_BASE;
                    SetPixel(pixels, size, x: 48 + dx, y: 48 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // ============== 腿部绘制 ==============
        // 右腿内层：x=0..15, y=16..31
        private static void FillLegRight(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 16; dx++)
                {
                    var c = dy >= 12 ? SHOE : PANTS_BASE;
                    SetPixel(pixels, size, x: 0 + dx, y: 16 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // 右腿外层：x=0..15, y=32..47
        private static void FillLegRightOverlay(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 16; dx++)
                {
                    var c = dy >= 12 ? SHOE : PANTS_SHADOW;
                    SetPixel(pixels, size, x: 0 + dx, y: 32 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // 左腿内层：x=16..31, y=48..63
        private static void FillLegLeft(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 16; dx++)
                {
                    var c = dy >= 12 ? SHOE : PANTS_BASE;
                    SetPixel(pixels, size, x: 16 + dx, y: 48 + dy, c.r, c.g, c.b, c.a);
                }
        }

        // 左腿外层：x=0..15, y=48..63
        private static void FillLegLeftOverlay(byte[] pixels, int size)
        {
            for (int dy = 0; dy < 16; dy++)
                for (int dx = 0; dx < 16; dx++)
                {
                    var c = dy >= 12 ? SHOE : PANTS_SHADOW;
                    SetPixel(pixels, size, x: 0 + dx, y: 48 + dy, c.r, c.g, c.b, c.a);
                }
        }

        /// <summary>设置单个像素（RGBA → 内部存为 BGRA 小端，这里直接写 BGRA 顺序）</summary>
        private static void SetPixel(byte[] pixels, int size, int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            int idx = (y * size + x) * 4;
            pixels[idx + 0] = b;   // B
            pixels[idx + 1] = g;   // G
            pixels[idx + 2] = r;   // R
            pixels[idx + 3] = a;   // A
        }
    }
}
