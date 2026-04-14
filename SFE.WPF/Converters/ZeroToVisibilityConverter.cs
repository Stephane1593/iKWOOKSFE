// File: SFE.WPF/Converters/ZeroToVisibilityConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SFE.WPF.Converters;

/// <summary>
/// Shows the element when the value is 0 (or null), collapses it otherwise.
/// Used for "empty state" messages.
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Visible; // null → treat as empty
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}