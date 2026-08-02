namespace BlockMacro.Models;

public sealed class MouseMoveBlock : MacroBlock
{
    public int X { get; set; }
    public int Y { get; set; }

    public override string DisplayName => "Mouse Move";

    public override string Summary => $"({X}, {Y})";

    public override MacroBlock Clone() => new MouseMoveBlock
    {
        X = X,
        Y = Y
    };
}
