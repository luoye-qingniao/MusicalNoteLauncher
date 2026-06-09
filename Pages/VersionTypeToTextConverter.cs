using System;
using System.Globalization;

namespace MusicalNoteLauncher.Pages
{
    public class VersionTypeToTextConverter : SafeConverterBase<VersionTypeToTextConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return "未知";
            }
            if (string.Equals(text, "release", StringComparison.OrdinalIgnoreCase))
            {
                return "正式版";
            }
            if (string.Equals(text, "snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return "快照版";
            }
            return text;
        }
    }
}


