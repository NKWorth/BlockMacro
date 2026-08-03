using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BlockMacro;

public sealed class SelectedBlockBackgroundConverter : IMultiValueConverter
{
    public static SelectedBlockBackgroundConverter Instance { get; } = new();

    private static readonly SolidColorBrush SelectedBrush = CreateBrush("#DBEAFE");
    private static readonly SolidColorBrush NormalBrush = CreateBrush("#F8FAFC");

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && ReferenceEquals(values[0], values[1]))
        {
            return SelectedBrush;
        }

        return NormalBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
