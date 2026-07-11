using System;
using System.Globalization;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
    public class StatusToBackgroundConverter : SafeConverterBase<StatusToBackgroundConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return new SolidColorBrush(Color.FromRgb(33, 150, 243));
            }
            if (text == "已下载")
            {
                return new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            if (text == "下载中")
            {
                return new SolidColorBrush(Color.FromRgb(255, 152, 0));
            }
            return new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }
    }
}


