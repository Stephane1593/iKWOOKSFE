using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SFE.WPF.Converters
{
    public class WidthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth && double.TryParse(parameter?.ToString(), out double minWidthThreshold))
            {
                return actualWidth >= minWidthThreshold ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}