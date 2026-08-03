namespace MacroBlocks.Models;

/// <summary>
/// Legacy marker kept for deserializing older scripts. Nested Children replace this in the UI.
/// </summary>
public sealed class EndContinueBlock : MacroBlock
{
    public override string DisplayName => "End Continue";

    public override string Summary => "legacy end marker";

    public override MacroBlock Clone() => new EndContinueBlock();
}
