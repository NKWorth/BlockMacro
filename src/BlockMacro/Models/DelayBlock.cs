namespace BlockMacro.Models;

public sealed class DelayBlock : MacroBlock
{
    public int Milliseconds { get; set; } = 500;

    public override string DisplayName => "Delay";

    public override string Summary => $"{Milliseconds} ms";

    public override MacroBlock Clone() => new DelayBlock
    {
        Milliseconds = Milliseconds
    };
}
