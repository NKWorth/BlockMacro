namespace MacroBlocks.Models.Actions;

public sealed class MouseClickBlock : ActionBlock
{
    private int _x;
    private int _y;
    private MouseButton _button = MouseButton.Left;
    private int _clickCount = 1;

    public int X
    {
        get => _x;
        set
        {
            if (SetField(ref _x, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            if (SetField(ref _y, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public MouseButton Button
    {
        get => _button;
        set
        {
            if (SetField(ref _button, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public int ClickCount
    {
        get => _clickCount;
        set
        {
            if (SetField(ref _clickCount, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

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
