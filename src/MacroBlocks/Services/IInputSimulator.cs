using MacroBlocks.Models;

namespace MacroBlocks.Services;

public interface IInputSimulator
{
    void MoveMouse(int x, int y);

    /// <summary>
    /// Moves the cursor to (<paramref name="x"/>, <paramref name="y"/>).
    /// When <paramref name="durationMilliseconds"/> is 0, jumps instantly;
    /// otherwise lerps from the current cursor position over that duration.
    /// </summary>
    Task MoveMouseAsync(int x, int y, int durationMilliseconds, CancellationToken cancellationToken = default);

    void Click(int x, int y, MouseButton button, int clickCount);
    void KeyPress(ushort virtualKey, bool ctrl, bool alt, bool shift);
}
