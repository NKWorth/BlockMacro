using System.Drawing;
using System.IO;
using MacroBlocks.Models.Actions;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MacroBlocks.Services.Vision;

public readonly record struct ImageMatchResult(bool Found, double Score, int X, int Y);

/// <summary>
/// Template-matches a reference image against a captured search region.
/// </summary>
public interface IImageMatcher
{
    ImageMatchResult Find(FindImageBlock block, string imagesDirectory);
}

public sealed class OpenCvImageMatcher : IImageMatcher
{
    public ImageMatchResult Find(FindImageBlock block, string imagesDirectory)
    {
        if (string.IsNullOrWhiteSpace(block.ImageFileName))
        {
            throw new InvalidOperationException("Find Image has no template image selected.");
        }

        var path = Path.Combine(imagesDirectory, block.ImageFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Template image not found: {block.ImageFileName}", path);
        }

        using var templateBmp = new Bitmap(path);
        using var haystackBmp = CaptureHaystack(block);

        if (templateBmp.Width > haystackBmp.Width || templateBmp.Height > haystackBmp.Height)
        {
            return new ImageMatchResult(false, 0, 0, 0);
        }

        using var haystack = BitmapConverter.ToMat(haystackBmp);
        using var template = BitmapConverter.ToMat(templateBmp);
        using var result = new Mat();

        Cv2.MatchTemplate(haystack, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

        var found = maxVal >= block.Confidence;
        return new ImageMatchResult(found, maxVal, maxLoc.X, maxLoc.Y);
    }

    private static Bitmap CaptureHaystack(FindImageBlock block)
        => block.Scope switch
        {
            ImageSearchScope.FullVirtualScreen => ScreenCapture.CaptureVirtualScreen(),
            ImageSearchScope.PrimaryMonitor => ScreenCapture.CapturePrimaryMonitor(),
            ImageSearchScope.ScreenRegion => ScreenCapture.CaptureScreenRect(
                block.RegionX, block.RegionY, block.RegionWidth, block.RegionHeight),
            ImageSearchScope.ApplicationWindow => CaptureWindow(block, region: false),
            ImageSearchScope.ApplicationWindowRegion => CaptureWindow(block, region: true),
            _ => ScreenCapture.CaptureVirtualScreen()
        };

    private static Bitmap CaptureWindow(FindImageBlock block, bool region)
    {
        var hwnd = WindowLocator.FindWindow(block.WindowTitleContains, block.WindowProcessName);
        if (hwnd == IntPtr.Zero)
        {
            var hint = string.IsNullOrWhiteSpace(block.WindowTitleContains)
                && string.IsNullOrWhiteSpace(block.WindowProcessName)
                ? "Set a window title and/or process name."
                : $"No window matched title '{block.WindowTitleContains}' / process '{block.WindowProcessName}'.";
            throw new InvalidOperationException(hint);
        }

        return region
            ? ScreenCapture.CaptureWindowClientRegion(
                hwnd, block.RegionX, block.RegionY, block.RegionWidth, block.RegionHeight)
            : ScreenCapture.CaptureWindowClient(hwnd);
    }
}
