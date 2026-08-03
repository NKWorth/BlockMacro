namespace MacroBlocks.Models;

public sealed class DelayBlock : MacroBlock
{
    private int _milliseconds = 500;

    public int Milliseconds
    {
        get => _milliseconds;
        set
        {
            if (SetField(ref _milliseconds, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Delay";

    public override string Summary => $"{Milliseconds} ms";

    public override MacroBlock Clone() => new DelayBlock
    {
        Milliseconds = Milliseconds
    };
}
