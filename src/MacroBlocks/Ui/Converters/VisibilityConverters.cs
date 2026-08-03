using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MacroBlocks.Ui.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static BoolToVisibilityConverter Instance { get; } = new();

    public static BoolToVisibilityConverter Inverted { get; } = new() { Invert = true };

    public bool Invert { get; init; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>Visible when the value is not null.</summary>
    public static NullToVisibilityConverter Instance { get; } = new();

    /// <summary>Visible when the value is null.</summary>
    public static NullToVisibilityConverter WhenNull { get; } = new() { VisibleWhenNull = true };

    public bool VisibleWhenNull { get; init; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value is null;
        var visible = VisibleWhenNull ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
