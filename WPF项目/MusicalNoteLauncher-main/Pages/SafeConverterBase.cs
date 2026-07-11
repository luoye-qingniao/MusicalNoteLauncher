using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public abstract class SafeConverterBase<T> : MarkupExtension, IValueConverter where T : class, new()
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            try
            {
                return this;
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] " + typeof(T).Name + ".ProvideValue失败: " + ex.Message);
                return Activator.CreateInstance<T>();
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return ConvertSafe(value, targetType, parameter, culture);
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] " + typeof(T).Name + ".Convert失败: " + ex.Message);
                return GetDefaultValue(targetType);
            }
        }

        protected abstract object ConvertSafe(object value, Type targetType, object parameter, CultureInfo culture);

        protected virtual object GetDefaultValue(Type targetType)
        {
            if (targetType == typeof(Visibility))
                return Visibility.Collapsed;
            if (targetType == typeof(bool))
                return false;
            if (targetType == typeof(double) || targetType == typeof(int))
                return 0;
            if (typeof(Brush).IsAssignableFrom(targetType))
                return new SolidColorBrush(Colors.Transparent);
            return null;
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


