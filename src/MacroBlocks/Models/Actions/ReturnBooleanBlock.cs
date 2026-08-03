namespace MacroBlocks.Models.Actions;

/// <summary>
/// Sets the Boolean output of the current script run (last write wins).
/// Used by Flow Graph If nodes via a Condition edge from a RunScript node.
/// </summary>
public sealed class ReturnBooleanBlock : ActionBlock
{
    private bool _value;

    public bool Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Return Boolean";

    public override string Summary => Value ? "true" : "false";

    public override MacroBlock Clone() => new ReturnBooleanBlock
    {
        Value = Value
    };
}
