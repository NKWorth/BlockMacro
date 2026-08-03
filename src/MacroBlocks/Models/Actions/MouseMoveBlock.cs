namespace MacroBlocks.Models.Actions;

public sealed class MouseMoveBlock : ActionBlock
{
    private int _x;
    private int _y;
    private int _durationMilliseconds;

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

    /// <summary>
    /// How long the cursor should take to reach the target. 0 = instant jump.
    /// </summary>
    public int DurationMilliseconds
    {
        get => _durationMilliseconds;
        set
        {
            if (SetField(ref _durationMilliseconds, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Mouse Move";

    public override string Summary =>
        DurationMilliseconds <= 0
            ? $"({X}, {Y}) · instant"
            : $"({X}, {Y}) · {DurationMilliseconds} ms";

    public override MacroBlock Clone() => new MouseMoveBlock
    {
        X = X,
        Y = Y,
        DurationMilliseconds = DurationMilliseconds
    };
}
