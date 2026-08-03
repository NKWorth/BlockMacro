using System.Diagnostics;
using MacroBlocks.Models;
using MacroBlocks.Native;

namespace MacroBlocks.Services;

/// <summary>
/// Windows SendInput-based mouse/keyboard injection.
/// </summary>
public sealed class Win32InputSimulator : IInputSimulator
{
    private const int LerpStepMilliseconds = 8;

    public void MoveMouse(int x, int y)
    {
        Send(CreateMouseMove(x, y));
    }

    public async Task MoveMouseAsync(
        int x,
        int y,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
    {
        if (durationMilliseconds <= 0)
        {
            MoveMouse(x, y);
            return;
        }

        if (!NativeMethods.GetCursorPos(out var start))
        {
            MoveMouse(x, y);
            return;
        }

        var sw = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMilliseconds, 0.0, 1.0);
            var currentX = (int)Math.Round(start.X + (x - start.X) * t);
            var currentY = (int)Math.Round(start.Y + (y - start.Y) * t);
            MoveMouse(currentX, currentY);

            if (t >= 1.0)
            {
                break;
            }

            await Task.Delay(LerpStepMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        // Guarantee the exact target after the last interpolated frame.
        MoveMouse(x, y);
    }

    public void Click(int x, int y, MouseButton button, int clickCount)
    {
        MoveMouse(x, y);

        var (down, up) = button switch
        {
            MouseButton.Right => (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp),
            MouseButton.Middle => (NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp),
            _ => (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp)
        };

        for (var i = 0; i < Math.Max(1, clickCount); i++)
        {
            Send(CreateMouseButton(down), CreateMouseButton(up));
        }
    }

    public void KeyPress(ushort virtualKey, bool ctrl, bool alt, bool shift)
    {
        var downs = new List<NativeMethods.INPUT>();
        var ups = new List<NativeMethods.INPUT>();

        if (ctrl)
        {
            downs.Add(CreateKey(NativeMethods.VkControl, down: true));
            ups.Add(CreateKey(NativeMethods.VkControl, down: false));
        }

        if (alt)
        {
            downs.Add(CreateKey(NativeMethods.VkMenu, down: true));
            ups.Add(CreateKey(NativeMethods.VkMenu, down: false));
        }

        if (shift)
        {
            downs.Add(CreateKey(NativeMethods.VkShift, down: true));
            ups.Add(CreateKey(NativeMethods.VkShift, down: false));
        }

        downs.Add(CreateKey(virtualKey, down: true));
        ups.Add(CreateKey(virtualKey, down: false));

        // Release modifiers in reverse order.
        ups.Reverse();
        Send(downs.Concat(ups).ToArray());
    }

    private static NativeMethods.INPUT CreateMouseMove(int x, int y)
    {
        var screenW = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen));
        var screenH = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen));

        // Absolute coords are normalized to 0..65535.
        var absX = (int)Math.Round(x * 65535.0 / (screenW - 1));
        var absY = (int)Math.Round(y * 65535.0 / (screenH - 1));

        return new NativeMethods.INPUT
        {
            type = NativeMethods.InputMouse,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = NativeMethods.MouseEventMove | NativeMethods.MouseEventAbsolute
                }
            }
        };
    }

    private static NativeMethods.INPUT CreateMouseButton(uint flags) => new()
    {
        type = NativeMethods.InputMouse,
        U = new NativeMethods.InputUnion
        {
            mi = new NativeMethods.MOUSEINPUT
            {
                dwFlags = flags
            }
        }
    };

    private static NativeMethods.INPUT CreateKey(ushort virtualKey, bool down) => new()
    {
        type = NativeMethods.InputKeyboard,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = down ? 0u : NativeMethods.KeyEventKeyUp
            }
        }
    };

    private static void Send(params NativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        _ = NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize);
    }

    private static readonly int MarshalSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
}
