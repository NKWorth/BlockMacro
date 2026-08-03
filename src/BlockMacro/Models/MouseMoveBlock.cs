namespace BlockMacro.Models;

public sealed class MouseMoveBlock : MacroBlock
{
    private int _x;
    private int _y;

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

    public override string DisplayName => "Mouse Move";

    public override string Summary => $"({X}, {Y})";

    public override MacroBlock Clone() => new MouseMoveBlock
    {
        X = X,
        Y = Y
    };
}
