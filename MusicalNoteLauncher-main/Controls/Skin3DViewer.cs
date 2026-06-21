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
        private BitmapSource _skinBitmapOpaque; // 内层立方体使用的无Alpha版本
        private bool _isSlim;

        private const double HEAD_SIZE = 8.0;
        private const double BODY_W = 8.0;
        private const double BODY_H = 12.0;
        private const double BODY_D = 4.0;
        private const double LEG_H = 12.0;
        private const double LEG_W = 4.0;
        private const double LEG_D = 4.0;
        private const double ARM_H = 12.0;
        private const double ARM_D = 4.0;

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
            _skinBitmapOpaque = null;
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
                _skinBitmapOpaque = null;
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
            // 为内层立方体创建无Alpha版本（HMCL的PhongMaterial忽略Alpha，WPF的DiffuseMaterial尊重Alpha）
            _skinBitmapOpaque = MakeOpaque(processed);

            // 通过 PNG 往返转换，将 WriteableBitmap 转为标准 BitmapImage
            // 消除 WPF 内部可能存在的 WriteableBitmap -> ImageBrush 渲染差异
            using (var ms = new System.IO.MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_skinBitmapOpaque));
                encoder.Save(ms);
                ms.Position = 0;

                var bmpImage = new BitmapImage();
                bmpImage.BeginInit();
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.StreamSource = ms;
                bmpImage.EndInit();
                bmpImage.Freeze();
                _skinBitmapOpaque = bmpImage;
            }

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

            var result = WriteableBitmap.Create(dstW, dstH, 96, 96, PixelFormats.Bgra32, null, dstPixels, dstW * 4);
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

        private static BitmapSource MakeOpaque(BitmapSource src)
        {
            int w = src.PixelWidth;
            int h = src.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[h * stride];
            src.CopyPixels(pixels, stride, 0);
            // HMCL的PhongMaterial: 透明像素显示为默认diffuseColor(白色)
            // 因此将透明像素填充为白色，而不是保留黑色RGB
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0)
                {
                    pixels[i] = 255;     // B
                    pixels[i + 1] = 255; // G
                    pixels[i + 2] = 255; // R
                }
                pixels[i + 3] = 255;     // A
            }
            var result = WriteableBitmap.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
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

            var result = WriteableBitmap.Create(dstW, dstH, 96, 96, PixelFormats.Bgra32, null, dstPixels, dstW * 4);
            return result;
        }

        private void RebuildModel()
        {
            ClearModel();
            if (_skinBitmap == null) return;

            double armW = _isSlim ? 3.0 : 4.0;

            var imageBrush = new ImageBrush(_skinBitmapOpaque);
            imageBrush.Stretch = Stretch.Fill;

            // 头（Y=+10，上方）- 内层头（脸部）位于皮肤文件 (0,0)-(32,16)
            _modelGroup.Children.Add(CreatePart(
                new Size3D(HEAD_SIZE, HEAD_SIZE, HEAD_SIZE),
                new Point(0.0 / 64.0, 0.0 / 64.0), new Size(32.0 / 64.0, 16.0 / 64.0),
                new Point3D(0, +(BODY_H + HEAD_SIZE) / 2.0, 0), imageBrush));

            // 身体（Y=0，中间）
            _modelGroup.Children.Add(CreatePart(
                new Size3D(BODY_W, BODY_H, BODY_D),
                new Point(16.0 / 64.0, 16.0 / 64.0), new Size(24.0 / 64.0, 16.0 / 64.0),
                new Point3D(0, 0, 0), imageBrush));

            // 右臂（右边，Y=0）
            _modelGroup.Children.Add(CreatePart(
                new Size3D(armW, ARM_H, ARM_D),
                new Point(40.0 / 64.0, 16.0 / 64.0), new Size((_isSlim ? 14.0 : 16.0) / 64.0, 16.0 / 64.0),
                new Point3D(+(BODY_W + armW) / 2.0, 0, 0), imageBrush));

            // 左臂（左边，Y=0）
            _modelGroup.Children.Add(CreatePart(
                new Size3D(armW, ARM_H, ARM_D),
                new Point(32.0 / 64.0, 48.0 / 64.0), new Size((_isSlim ? 14.0 : 16.0) / 64.0, 16.0 / 64.0),
                new Point3D(-(BODY_W + armW) / 2.0, 0, 0), imageBrush));

            // 右腿（右下方，Y=-12）
            _modelGroup.Children.Add(CreatePart(
                new Size3D(LEG_W, LEG_H, LEG_D),
                new Point(0.0 / 64.0, 16.0 / 64.0), new Size(16.0 / 64.0, 16.0 / 64.0),
                new Point3D(+(BODY_W - LEG_W) / 2.0, -(BODY_H + LEG_H) / 2.0, 0), imageBrush));

            // 左腿（左下方，Y=-12）
            _modelGroup.Children.Add(CreatePart(
                new Size3D(LEG_W, LEG_H, LEG_D),
                new Point(16.0 / 64.0, 48.0 / 64.0), new Size(16.0 / 64.0, 16.0 / 64.0),
                new Point3D(-(BODY_W - LEG_W) / 2.0, -(BODY_H + LEG_H) / 2.0, 0), imageBrush));

            // 外层模型（外层皮肤）- 外层头（帽子）位于皮肤文件 (32,0)-(64,16)
            var headOuter = CreateMultiCubesPart(
                8, 8, 8, 32.0 / 64.0, 0.0 / 64.0,
                new Point3D(0, +(BODY_H + HEAD_SIZE) / 2.0, 0), 1.125, 0.2);
            if (headOuter != null) _modelGroup.Children.Add(headOuter);

            var bodyOuter = CreateMultiCubesPart(
                8, 12, 4, 16.0 / 64.0, 32.0 / 64.0,
                new Point3D(0, 0, 0), 1.0, 0.2);
            if (bodyOuter != null) _modelGroup.Children.Add(bodyOuter);

            var rightArmOuterW = _isSlim ? 3 : 4;
            var rightArmOuter = CreateMultiCubesPart(
                rightArmOuterW, 12, 4, 40.0 / 64.0, 32.0 / 64.0,
                new Point3D(+(BODY_W + armW) / 2.0, 0, 0), 1.0625, 0.2);
            if (rightArmOuter != null) _modelGroup.Children.Add(rightArmOuter);

            var leftArmOuter = CreateMultiCubesPart(
                rightArmOuterW, 12, 4, 48.0 / 64.0, 48.0 / 64.0,
                new Point3D(-(BODY_W + armW) / 2.0, 0, 0), 1.0625, 0.2);
            if (leftArmOuter != null) _modelGroup.Children.Add(leftArmOuter);

            var rightLegOuter = CreateMultiCubesPart(
                4, 12, 4, 0.0 / 64.0, 32.0 / 64.0,
                new Point3D(+(BODY_W - LEG_W) / 2.0, -(BODY_H + LEG_H) / 2.0, 0), 1.0625, 0.2);
            if (rightLegOuter != null) _modelGroup.Children.Add(rightLegOuter);

            var leftLegOuter = CreateMultiCubesPart(
                4, 12, 4, 0.0 / 64.0, 48.0 / 64.0,
                new Point3D(-(BODY_W - LEG_W) / 2.0, -(BODY_H + LEG_H) / 2.0, 0), 1.0625, 0.2);
            if (leftLegOuter != null) _modelGroup.Children.Add(leftLegOuter);
        }

        private Model3D CreatePart(Size3D size, Point uvStart, Size uvScale, Point3D offset, ImageBrush brush)
        {
            // HMCL: Y向下，WPF: Y向上
            // 在BuildCubeMeshHMCL中直接翻转顶点Y值
            var mesh = BuildCubeMeshHMCL(size.X, size.Y, size.Z, uvStart.X, uvStart.Y, uvScale.Width, uvScale.Height);
            var material = new DiffuseMaterial(brush);
            var model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;
            model.Transform = new TranslateTransform3D(offset.X, offset.Y, offset.Z);
            return model;
        }

        private MeshGeometry3D BuildCubeMeshHMCL(double w, double h, double d,
            double startX, double startY, double scaleX, double scaleY)
        {
            // === HMCL createPoints (Y向下) -> WPF (Y向上)
            // HMCL中Y=-h表示上方（因为Y向下，-h表示向上移动）
            // WPF中Y=+h表示上方（因为Y向上）
            // 所以：将所有顶点的Y值取反
            double hw = w / 2.0, hh = h / 2.0, hd = d / 2.0;

            var positions = new Point3D[]
            {
                new Point3D(-hw, +hh, +hd), // P0 前左上（Y取反）
                new Point3D(+hw, +hh, +hd), // P1 前右上（Y取反）
                new Point3D(-hw, -hh, +hd), // P2 前左下（Y取反）
                new Point3D(+hw, -hh, +hd), // P3 前右下（Y取反）
                new Point3D(-hw, +hh, -hd), // P4 后左上（Y取反）
                new Point3D(+hw, +hh, -hd), // P5 后右上（Y取反）
                new Point3D(-hw, -hh, -hd), // P6 后左下（Y取反）
                new Point3D(+hw, -hh, -hd), // P7 后右下（Y取反）
            };

            // === HMCL createTexCoords (Y向下)
            double totalX = (w + d) * 2.0;
            double totalY = h + d;
            double half_width = w / totalX * scaleX;
            double half_depth = d / totalX * scaleX;
            double top_x = d / totalX * scaleX + startX;
            double bottom_x = startX;
            double top_y = startY;
            double middle_y = d / totalY * scaleY + top_y;
            double bottom_y = scaleY + top_y;
            double arm4 = half_width;

            var uvPoints = new Point[]
            {
                new Point(top_x, top_y),                                    // T0
                new Point(top_x + half_width, top_y),                      // T1
                new Point(top_x + half_width * 2, top_y),                  // T2
                new Point(bottom_x, middle_y),                               // T3
                new Point(bottom_x + half_depth, middle_y),                 // T4
                new Point(bottom_x + half_depth + half_width, middle_y),       // T5
                new Point(bottom_x + scaleX - arm4, middle_y),             // T6
                new Point(bottom_x + scaleX, middle_y),                      // T7
                new Point(bottom_x, bottom_y),                               // T8
                new Point(bottom_x + half_depth, bottom_y),                 // T9
                new Point(bottom_x + half_depth + half_width, bottom_y),     // T10
                new Point(bottom_x + scaleX - arm4, bottom_y),               // T11
                new Point(bottom_x + scaleX, bottom_y),                      // T12
            };

            // === HMCL createFaces (12个三角形 - 6个面 x 2三角形)
            // 格式：每个三角形3个顶点 [pointIdx, texIdx]
            int[][] faces = new int[][]
            {
                // TOP
                new int[] {5, 0,  4, 1,  0, 5},    // P5,T0, P4,T1, P0,T5
                new int[] {5, 0,  0, 5,  1, 4},      // P5,T0, P0,T5, P1,T4
                // RIGHT
                new int[] {5, 3,  1, 4,  3, 9},    // P5,T3, P1,T4, P3,T9
                new int[] {5, 3,  3, 9,  7, 8},      // P5,T3, P3,T9, P7,T8
                // FRONT
                new int[] {1, 4,  0, 5,  2, 10},     // P1,T4, P0,T5, P2,T10
                new int[] {1, 4,  2, 10,  3, 9},       // P1,T4, P2,T10, P3,T9
                // LEFT
                new int[] {0, 5,  4, 6,  6, 11},       // P0,T5, P4,T6, P6,T11
                new int[] {0, 5,  6, 11,  2, 10},      // P0,T5, P6,T11, P2,T10
                // BACK
                new int[] {4, 6,  5, 7,  7, 12},       // P4,T6, P5,T7, P7,T12
                new int[] {4, 6,  7, 12,  6, 11},      // P4,T6, P7,T12, P6,T11
                // BOTTOM
                new int[] {3, 5,  2, 6,  6, 2},       // P3,T5, P2,T6, P6,T2
                new int[] {3, 5,  6, 2,  7, 1},        // P3,T5, P6,T2, P7,T1
            };

            var mesh = new MeshGeometry3D();

            // 生成12个三角形（6面 × 2三角形），BackMaterial 负责双面渲染
            // 移除冗余反面三角形，避免 z-fighting 导致黑块闪烁
            int triIdx = 0;
            foreach (var face in faces)
            {
                for (int i = 0; i < 3; i++)
                {
                    int pIdx = face[i * 2];
                    int tIdx = face[i * 2 + 1];
                    mesh.Positions.Add(positions[pIdx]);
                    mesh.TextureCoordinates.Add(uvPoints[tIdx]);
                    mesh.TriangleIndices.Add(triIdx++);
                }
            }

            return mesh;
        }

        private Model3DGroup CreateMultiCubesPart(
            int width, int height, int depth,
            double startX, double startY,
            Point3D offset, double length, double thick)
        {
            if (_skinBitmap == null) return null;

            int texW = _skinBitmap.PixelWidth;
            int texH = _skinBitmap.PixelHeight;
            int interval = Math.Max(texW / 64, 1);

            int start_x = (int)(startX * texW);
            int start_y = (int)(startY * texH);

            var pixels = new byte[texW * texH * 4];
            _skinBitmap.CopyPixels(pixels, texW * 4, 0);

            var group = new Model3DGroup();

            // FRONT（WPF Y向上，与HMCL Y向下相反，Y坐标取反）
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x + depth * interval, start_y + depth * interval,
                width, height, interval, false, false,
                (x, y) => new Point3D(
                    ((width - 1) / 2.0 - x) * length,
                    ((height - 1) / 2.0 - y) * length,
                    (depth * length + thick) / 2.0),
                new Size3D(length, length, thick));

            // BACK
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x + width * interval + depth * interval * 2, start_y + depth * interval,
                width, height, interval, true, false,
                (x, y) => new Point3D(
                    ((width - 1) / 2.0 - x) * length,
                    ((height - 1) / 2.0 - y) * length,
                    -(depth * length + thick) / 2.0),
                new Size3D(length, length, thick));

            // LEFT
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x + width * interval + depth * interval, start_y + depth * interval,
                depth, height, interval, false, false,
                (x, y) => new Point3D(
                    -(width * length + thick) / 2.0,
                    ((height - 1) / 2.0 - y) * length,
                    ((depth - 1) / 2.0 - x) * length),
                new Size3D(thick, length, length));

            // RIGHT
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x, start_y + depth * interval,
                depth, height, interval, true, false,
                (x, y) => new Point3D(
                    (width * length + thick) / 2.0,
                    ((height - 1) / 2.0 - y) * length,
                    ((depth - 1) / 2.0 - x) * length),
                new Size3D(thick, length, length));

            // TOP（HMCL中Y=-h为上，WPF中Y=+h为上，所以Y值取反）
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x + depth * interval, start_y,
                width, depth, interval, false, false,
                (x, y) => new Point3D(
                    ((width - 1) / 2.0 - x) * length,
                    (height * length + thick) / 2.0,
                    -((depth - 1) / 2.0 - y) * length),
                new Size3D(length, thick, length));

            // BOTTOM
            CreateFaceBoxes(group, pixels, texW, texH,
                start_x + width * interval + depth * interval, start_y,
                width, depth, interval, false, false,
                (x, y) => new Point3D(
                    ((width - 1) / 2.0 - x) * length,
                    -(height * length + thick) / 2.0,
                    -((depth - 1) / 2.0 - y) * length),
                new Size3D(length, thick, length));

            group.Transform = new TranslateTransform3D(offset.X, offset.Y, offset.Z);
            return group;
        }

        private void CreateFaceBoxes(Model3DGroup group, byte[] pixels, int texW, int texH,
            int regionStartX, int regionStartY, int width, int height, int interval,
            bool reverseX, bool reverseY,
            Func<int, int, Point3D> positionFunc, Size3D boxSize)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int texX = regionStartX + (reverseX ? (width - x - 1) : x) * interval;
                    int texY = regionStartY + (reverseY ? (height - y - 1) : y) * interval;

                    if (texX < 0 || texX >= texW || texY < 0 || texY >= texH) continue;

                    int alpha = pixels[(texY * texW + texX) * 4 + 3];
                    if (alpha == 0) continue;

                    byte b = pixels[(texY * texW + texX) * 4 + 0];
                    byte g = pixels[(texY * texW + texX) * 4 + 1];
                    byte r = pixels[(texY * texW + texX) * 4 + 2];

                    var color = Color.FromRgb(r, g, b);
                    var center = positionFunc(x, y);
                    var box = BuildBoxMesh(center, boxSize);
                    var material = new DiffuseMaterial(new SolidColorBrush(color));
                    var model = new GeometryModel3D(box, material);
                    model.BackMaterial = material;
                    group.Children.Add(model);
                }
            }
        }

        private MeshGeometry3D BuildBoxMesh(Point3D center, Size3D size)
        {
            var mesh = new MeshGeometry3D();

            double hx = size.X / 2.0, hy = size.Y / 2.0, hz = size.Z / 2.0;
            double cx = center.X, cy = center.Y, cz = center.Z;

            var positions = new Point3D[]
            {
                new Point3D(cx - hx, cy - hy, cz + hz),
                new Point3D(cx + hx, cy - hy, cz + hz),
                new Point3D(cx - hx, cy + hy, cz + hz),
                new Point3D(cx + hx, cy + hy, cz + hz),
                new Point3D(cx - hx, cy - hy, cz - hz),
                new Point3D(cx + hx, cy - hy, cz - hz),
                new Point3D(cx - hx, cy + hy, cz - hz),
                new Point3D(cx + hx, cy + hy, cz - hz),
            };

            int[][] faces = new int[][]
            {
                new int[] {0, 1, 2, 1, 3, 2},
                new int[] {5, 4, 7, 4, 6, 7},
                new int[] {4, 0, 6, 0, 2, 6},
                new int[] {1, 5, 3, 5, 7, 3},
                new int[] {2, 3, 6, 3, 7, 6},
                new int[] {4, 5, 0, 5, 1, 0},
            };

            for (int i = 0; i < 8; i++)
            {
                mesh.Positions.Add(positions[i]);
                mesh.TextureCoordinates.Add(new Point(0, 0));
            }

            foreach (var face in faces)
            {
                for (int i = 0; i < 6; i++)
                    mesh.TriangleIndices.Add(face[i]);
            }

            return mesh;
        }
    }

    public static class DefaultSkinGenerator
    {
        public static BitmapSource CreateSteveSkin()
        {
            const int size = 64;
            var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];

            for (int i = 0; i < pixels.Length; i += 4)
                pixels[i + 3] = 0;

            FillRect(pixels, size, 0, 0, 32, 16, 200, 160, 130);
            FillRect(pixels, size, 32, 0, 32, 16, 180, 140, 110);
            FillRect(pixels, size, 16, 16, 24, 16, 80, 120, 200);
            FillRect(pixels, size, 16, 32, 24, 16, 60, 100, 180);
            FillRect(pixels, size, 40, 16, 16, 16, 90, 130, 210);
            FillRect(pixels, size, 40, 32, 16, 16, 70, 110, 190);
            FillRect(pixels, size, 0, 16, 16, 16, 60, 70, 120);
            FillRect(pixels, size, 0, 32, 16, 16, 40, 50, 100);
            FillRect(pixels, size, 32, 48, 16, 16, 90, 130, 210);
            FillRect(pixels, size, 48, 48, 16, 16, 70, 110, 190);
            FillRect(pixels, size, 16, 48, 16, 16, 60, 70, 120);
            FillRect(pixels, size, 0, 48, 16, 16, 40, 50, 100);

            wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            return wb;
        }

        public static BitmapSource CreateAlexSkin()
        {
            const int size = 64;
            var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];

            for (int i = 0; i < pixels.Length; i += 4)
                pixels[i + 3] = 0;

            FillRect(pixels, size, 0, 0, 32, 16, 220, 180, 150);
            FillRect(pixels, size, 32, 0, 32, 16, 200, 160, 130);
            FillRect(pixels, size, 16, 16, 24, 16, 220, 80, 80);
            FillRect(pixels, size, 16, 32, 24, 16, 200, 60, 60);
            FillRect(pixels, size, 40, 16, 14, 16, 220, 180, 150);
            FillRect(pixels, size, 40, 32, 14, 16, 200, 160, 130);
            FillRect(pixels, size, 0, 16, 16, 16, 80, 60, 40);
            FillRect(pixels, size, 0, 32, 16, 16, 60, 40, 20);
            FillRect(pixels, size, 32, 48, 14, 16, 220, 180, 150);
            FillRect(pixels, size, 48, 48, 14, 16, 200, 160, 130);
            FillRect(pixels, size, 16, 48, 16, 16, 80, 60, 40);
            FillRect(pixels, size, 0, 48, 16, 16, 60, 40, 20);

            wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            return wb;
        }

        private static void FillRect(byte[] pixels, int stride, int x, int y, int w, int h, byte r, byte g, byte b)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    int idx = ((y + dy) * stride + (x + dx)) * 4;
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = 255;
                }
        }
    }
}
