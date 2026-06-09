using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public abstract class SafeMultiValueConverterBase<T> : MarkupExtension, IMultiValueConverter where T : class, new()
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

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return ConvertSafe(values, targetType, parameter, culture);
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] " + typeof(T).Name + ".Convert失败: " + ex.Message);
                return (targetType == typeof(double)) ? 0.0 : null;
            }
        }

        protected abstract object ConvertSafe(object[] values, Type targetType, object parameter, CultureInfo culture);

        public virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


