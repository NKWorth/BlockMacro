using BlockMacro.Models;

namespace BlockMacro.Services;

public interface IInputSimulator
{
    void MoveMouse(int x, int y);
    void Click(int x, int y, MouseButton button, int clickCount);
    void KeyPress(ushort virtualKey, bool ctrl, bool alt, bool shift);
}
