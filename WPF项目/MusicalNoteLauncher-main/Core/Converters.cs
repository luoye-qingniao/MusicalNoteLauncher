using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicalNoteLauncher.Core
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    public class VersionTypeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string type = value?.ToString() ?? "";
            switch (type)
            {
                case "正式版":
                case "release":
                    return new SolidColorBrush(Color.FromRgb(82, 193, 135));
                case "测试版":
                case "beta":
                    return new SolidColorBrush(Color.FromRgb(255, 184, 74));
                case "预览版":
                case "alpha":
                    return new SolidColorBrush(Color.FromRgb(230, 100, 100));
                default:
                    return new SolidColorBrush(Color.FromRgb(128, 128, 128));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long size && size > 0)
            {
                string[] units = { "B", "KB", "MB", "GB" };
                int unitIndex = 0;
                double sizeDouble = size;

                while (sizeDouble >= 1024 && unitIndex < units.Length - 1)
                {
                    sizeDouble /= 1024;
                    unitIndex++;
                }

                return $"大小: {sizeDouble:0.##} {units[unitIndex]}";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
