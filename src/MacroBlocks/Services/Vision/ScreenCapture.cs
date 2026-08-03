using System.Drawing;
using System.Drawing.Imaging;
using MacroBlocks.Native;

namespace MacroBlocks.Services.Vision;

/// <summary>
/// Captures screen or window bitmaps for image matching.
/// </summary>
public static class ScreenCapture
{
    public static Bitmap CaptureVirtualScreen()
    {
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SmXvVirtualScreen);
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SmYvVirtualScreen);
        var w = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var h = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);
        return CaptureScreenRect(x, y, w, h);
    }

    public static Bitmap CapturePrimaryMonitor()
    {
        var w = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var h = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return CaptureScreenRect(0, 0, w, h);
    }

    public static Bitmap CaptureScreenRect(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture region must be positive.");
        }

        var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static Bitmap CaptureWindowClient(IntPtr hWnd)
    {
        var (x, y, w, h) = WindowLocator.GetClientScreenBounds(hWnd);
        return CaptureScreenRect(x, y, w, h);
    }

    public static Bitmap CaptureWindowClientRegion(IntPtr hWnd, int relX, int relY, int width, int height)
    {
        var (originX, originY, clientW, clientH) = WindowLocator.GetClientScreenBounds(hWnd);
        var x = Math.Clamp(relX, 0, Math.Max(0, clientW - 1));
        var y = Math.Clamp(relY, 0, Math.Max(0, clientH - 1));
        var w = Math.Clamp(width, 1, clientW - x);
        var h = Math.Clamp(height, 1, clientH - y);
        return CaptureScreenRect(originX + x, originY + y, w, h);
    }
}
