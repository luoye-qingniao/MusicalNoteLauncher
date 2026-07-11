using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicalNoteLauncher.Core
{
    public class AccountTypeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AccountType type)
            {
                switch (type)
                {
                    case AccountType.Offline:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"));
                    case AccountType.Microsoft:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
                    case AccountType.AuthlibInjector:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B59B6"));
                    case AccountType.QingNiao:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                }
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
