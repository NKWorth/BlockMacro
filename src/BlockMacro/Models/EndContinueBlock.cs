namespace BlockMacro.Models;

/// <summary>
/// Closes a <see cref="ContinueUntilBlock"/> region in the flat script list.
/// </summary>
public sealed class EndContinueBlock : MacroBlock
{
    public override string DisplayName => "End Continue";

    public override string Summary => "end of continue-until body";

    public override MacroBlock Clone() => new EndContinueBlock();
}
