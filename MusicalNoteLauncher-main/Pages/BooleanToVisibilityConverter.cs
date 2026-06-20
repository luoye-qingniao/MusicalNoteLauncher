using System;
using System.Globalization;
using System.Windows;

namespace MusicalNoteLauncher.Pages
{
    public class BooleanToVisibilityConverter : SafeConverterBase<BooleanToVisibilityConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? flag = value as bool?;
            return (flag != null && flag.Value) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}


