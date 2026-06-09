using System;
using System.Globalization;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
    public class VersionTypeToColorConverter : SafeConverterBase<VersionTypeToColorConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return new SolidColorBrush(Color.FromRgb(156, 156, 156));
            }
            if (string.Equals(text, "release", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            if (string.Equals(text, "snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(255, 193, 7));
            }
            return new SolidColorBrush(Color.FromRgb(156, 156, 156));
        }
    }
}


