using System.Globalization;
using System.Windows.Data;

namespace BlockMacro.ViewModels;

/// <summary>
/// Inverts a bool for bindings such as disabling controls while running.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static InverseBoolConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
