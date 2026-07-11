using System;
using System.Globalization;

namespace MusicalNoteLauncher.Pages
{
    public class BooleanToAngleConverter : SafeConverterBase<BooleanToAngleConverter>
    {
        protected override object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? flag = value as bool?;
            return (flag != null && flag.Value) ? 90 : 0;
        }
    }
}


