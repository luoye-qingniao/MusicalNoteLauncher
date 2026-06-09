using System;
using System.Globalization;

namespace MusicalNoteLauncher.Pages
{
    public class StatusToEnabledConverter : SafeConverterBase<StatusToEnabledConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            return !string.IsNullOrEmpty(text) && text == "可下载";
        }
    }
}


