using System.Diagnostics;
using System.Text;
using MacroBlocks.Native;

namespace MacroBlocks.Services.Vision;

/// <summary>
/// Finds a top-level visible window by title substring and/or process name.
/// </summary>
public static class WindowLocator
{
    public static IntPtr FindWindow(string? titleContains, string? processName)
    {
        var titleFilter = string.IsNullOrWhiteSpace(titleContains)
            ? null
            : titleContains.Trim();
        string? processFilter = null;
        if (!string.IsNullOrWhiteSpace(processName))
        {
            processFilter = processName.Trim();
            if (processFilter.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processFilter = processFilter[..^4];
            }
        }

        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0 && titleFilter is not null)
            {
                return true;
            }

            var title = string.Empty;
            if (length > 0)
            {
                var sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                title = sb.ToString();
            }

            if (titleFilter is not null
                && title.IndexOf(titleFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            if (processFilter is not null)
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    var name = proc.ProcessName;
                    if (!name.Equals(processFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    return true;
                }
            }

            // Prefer normal app windows over empty-title shells when only process is specified.
            if (string.IsNullOrWhiteSpace(title) && titleFilter is null)
            {
                return true;
            }

            found = hWnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    public static (int X, int Y, int Width, int Height) GetClientScreenBounds(IntPtr hWnd)
    {
        if (!NativeMethods.GetClientRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("Could not read the target window client area.");
        }

        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        if (!NativeMethods.ClientToScreen(hWnd, ref origin))
        {
            throw new InvalidOperationException("Could not map the window client area to screen coordinates.");
        }

        return (origin.X, origin.Y, rect.Width, rect.Height);
    }
}
