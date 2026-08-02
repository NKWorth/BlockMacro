namespace BlockMacro.Models;

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public sealed class MouseClickBlock : MacroBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButton Button { get; set; } = MouseButton.Left;
    public int ClickCount { get; set; } = 1;

    public override string DisplayName => "Mouse Click";

    public override string Summary =>
        $"{Button} @ ({X}, {Y})" + (ClickCount > 1 ? $" ×{ClickCount}" : string.Empty);

    public override MacroBlock Clone() => new MouseClickBlock
    {
        X = X,
        Y = Y,
        Button = Button,
        ClickCount = ClickCount
    };
}
