using System;
using System.Globalization;

namespace MusicalNoteLauncher.Pages
{
    public class ProgressWidthConverter : SafeMultiValueConverterBase<ProgressWidthConverter>
    {
        protected override object ConvertSafe(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2)
            {
                if (values[0] is double num && values[1] is double num2)
                {
                    return num2 * (num / 100.0);
                }
            }
            return 0.0;
        }
    }
}


