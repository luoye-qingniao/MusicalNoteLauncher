using System;
using System.Globalization;
using System.Windows;

namespace MusicalNoteLauncher.Pages
{
    public class StatusToVisibilityConverter : SafeConverterBase<StatusToVisibilityConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (parameter as string == "downloading")
            {
                return (!string.IsNullOrEmpty(text) && text == "下载中") ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }
    }
}


