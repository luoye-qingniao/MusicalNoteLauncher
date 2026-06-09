using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public class VersionTypeToIconConverter : SafeConverterBase<VersionTypeToIconConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string a = value as string;
            if (string.Equals(a, "release", StringComparison.OrdinalIgnoreCase))
                return GetGrassBlockIcon();
            if (string.Equals(a, "snapshot", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "pre-release", StringComparison.OrdinalIgnoreCase))
                return GetCommandBlockIcon();
            return GetDirtBlockIcon();
        }

        private static DrawingImage GetGrassBlockIcon()
        {
            try
            {
                if (_grassBlockIcon == null)
                    _grassBlockIcon = CreateGrassBlockImage();
                return _grassBlockIcon;
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] 创建草方块图标失败: " + ex.Message);
                return null;
            }
        }

        private static DrawingImage GetCommandBlockIcon()
        {
            try
            {
                if (_commandBlockIcon == null)
                    _commandBlockIcon = CreateCommandBlockImage();
                return _commandBlockIcon;
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] 创建命令方块图标失败: " + ex.Message);
                return null;
            }
        }

        private static DrawingImage GetDirtBlockIcon()
        {
            try
            {
                if (_dirtBlockIcon == null)
                    _dirtBlockIcon = CreateDirtBlockImage();
                return _dirtBlockIcon;
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] 创建泥土方块图标失败: " + ex.Message);
                return null;
            }
        }

        private static DrawingImage CreateGrassBlockImage()
        {
            DrawingGroup drawingGroup = new DrawingGroup();
            int num = 2;
            int num2 = 16;
            for (int i = 0; i < num2; i++)
            {
                for (int j = 0; j < num2 / 2; j++)
                {
                    Color color;
                    if ((i + j) % 3 == 0)
                        color = Color.FromRgb(68, 156, 14);
                    else if ((i + j) % 5 == 0)
                        color = Color.FromRgb(86, 184, 26);
                    else
                        color = Color.FromRgb(74, 168, 14);
                    drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null,
                        new RectangleGeometry(new Rect(i * num, j * num, num, num))));
                }
            }
            for (int k = 0; k < num2; k++)
            {
                for (int l = num2 / 2; l < num2; l++)
                {
                    Color color2;
                    if (l - num2 / 2 < 3 && k % 2 == 0)
                        color2 = Color.FromRgb(86, 168, 14);
                    else if ((k + l * 3) % 7 == 0)
                        color2 = Color.FromRgb(134, 96, 67);
                    else if ((k + l * 2) % 5 == 0)
                        color2 = Color.FromRgb(115, 85, 54);
                    else
                        color2 = Color.FromRgb(124, 91, 60);
                    drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color2), null,
                        new RectangleGeometry(new Rect(k * num, l * num, num, num))));
                }
            }
            SolidColorBrush brush3 = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            drawingGroup.Children.Add(new GeometryDrawing(brush3, null, new RectangleGeometry(new Rect(30.0, 0.0, 2.0, 32.0))));
            drawingGroup.Children.Add(new GeometryDrawing(brush3, null, new RectangleGeometry(new Rect(0.0, 30.0, 32.0, 2.0))));
            DrawingImage drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            return drawingImage;
        }

        private static DrawingImage CreateCommandBlockImage()
        {
            DrawingGroup drawingGroup = new DrawingGroup();
            int num = 2;
            int num2 = 16;
            for (int i = 0; i < num2; i++)
            {
                for (int j = 0; j < num2; j++)
                {
                    Color color;
                    if (i == 0 || j == 0 || i == num2 - 1 || j == num2 - 1)
                        color = Color.FromRgb(111, 49, 138);
                    else if ((i + j) % 4 == 0)
                        color = Color.FromRgb(127, 55, 158);
                    else
                        color = Color.FromRgb(119, 59, 148);
                    drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null,
                        new RectangleGeometry(new Rect(i * num, j * num, num, num))));
                }
            }
            SolidColorBrush brush2 = new SolidColorBrush(Color.FromRgb(180, 75, 190));
            drawingGroup.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(4.0, 14.0, 16.0, 4.0))));
            for (int k = 0; k < 4; k++)
            {
                drawingGroup.Children.Add(new GeometryDrawing(brush2, null,
                    new RectangleGeometry(new Rect(16 + k * 2, 10 + k * 2, 2.0, 2.0))));
                drawingGroup.Children.Add(new GeometryDrawing(brush2, null,
                    new RectangleGeometry(new Rect(16 + k * 2, 22 - k * 2, 2.0, 2.0))));
            }
            drawingGroup.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(22.0, 14.0, 4.0, 4.0))));
            Color color2 = Color.FromRgb(255, 150, 255);
            drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color2), null, new RectangleGeometry(new Rect(6.0, 6.0, 2.0, 2.0))));
            drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color2), null, new RectangleGeometry(new Rect(10.0, 6.0, 2.0, 2.0))));
            DrawingImage drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            return drawingImage;
        }

        private static DrawingImage CreateDirtBlockImage()
        {
            DrawingGroup drawingGroup = new DrawingGroup();
            int num = 2;
            int num2 = 16;
            for (int i = 0; i < num2; i++)
            {
                for (int j = 0; j < num2; j++)
                {
                    int num3 = (i * 7 + j * 13) % 11;
                    Color color;
                    if (num3 == 0)
                        color = Color.FromRgb(132, 91, 61);
                    else if (num3 == 1)
                        color = Color.FromRgb(115, 81, 52);
                    else if (num3 == 2)
                        color = Color.FromRgb(143, 99, 66);
                    else if (num3 == 3)
                        color = Color.FromRgb(128, 86, 56);
                    else
                        color = Color.FromRgb(138, 94, 63);
                    drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null,
                        new RectangleGeometry(new Rect(i * num, j * num, num, num))));
                }
            }
            SolidColorBrush brush2 = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            drawingGroup.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(30.0, 0.0, 2.0, 32.0))));
            drawingGroup.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(0.0, 30.0, 32.0, 2.0))));
            DrawingImage drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            return drawingImage;
        }

        private static DrawingImage _grassBlockIcon;
        private static DrawingImage _commandBlockIcon;
        private static DrawingImage _dirtBlockIcon;
    }
}


