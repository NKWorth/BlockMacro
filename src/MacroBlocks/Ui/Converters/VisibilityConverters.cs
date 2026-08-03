using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MacroBlocks.Models.Graph;

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

public sealed class BoolToFontWeightConverter : IValueConverter
{
    public static BoolToFontWeightConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IfKindVisibilityConverter : IValueConverter
{
    public static IfKindVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FlowGraphNodeKind.If ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class RunScriptKindVisibilityConverter : IValueConverter
{
    public static RunScriptKindVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FlowGraphNodeKind.RunScript ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
