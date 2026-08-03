using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MacroBlocks.Ui.Drag;

/// <summary>
/// Semi-transparent floating preview that follows the cursor during a drag.
/// </summary>
internal sealed class DragGhost : IDisposable
{
    private readonly Popup _popup;
    private readonly Border _chrome;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private bool _disposed;

    public DragGhost()
    {
        _title = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
            FontSize = 13
        };
        _subtitle = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };

        _chrome = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            MinWidth = 140,
            Child = new StackPanel { Children = { _title, _subtitle } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                Opacity = 0.25,
                ShadowDepth = 2
            }
        };

        _popup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Absolute,
            IsHitTestVisible = false,
            Child = _chrome,
            IsOpen = false
        };
    }

    public void Show(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _subtitle.Visibility = string.IsNullOrWhiteSpace(subtitle)
            ? Visibility.Collapsed
            : Visibility.Visible;
        _popup.IsOpen = true;
        UpdatePosition();
    }

    public void UpdatePosition()
    {
        if (!_popup.IsOpen)
        {
            return;
        }

        var point = GetCursorPos();
        _popup.HorizontalOffset = point.X + 16;
        _popup.VerticalOffset = point.Y + 16;
    }

    public void Hide()
    {
        _popup.IsOpen = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Hide();
        _disposed = true;
    }

    private static Point GetCursorPos()
    {
        var w32 = new Native.NativeMethods.POINT();
        Native.NativeMethods.GetCursorPos(out w32);
        return new Point(w32.X, w32.Y);
    }
}
