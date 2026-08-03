namespace MacroBlocks.Models;

/// <summary>
/// Executes another saved script inline as a reusable subroutine.
/// </summary>
public sealed class RunSubscriptBlock : MacroBlock
{
    private Guid? _scriptId;
    private string _scriptName = "(no script)";

    public Guid? ScriptId
    {
        get => _scriptId;
        set
        {
            if (SetField(ref _scriptId, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string ScriptName
    {
        get => _scriptName;
        set
        {
            if (SetField(ref _scriptName, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Run Subscript";

    public override string Summary => ScriptName;

    public override MacroBlock Clone() => new RunSubscriptBlock
    {
        ScriptId = ScriptId,
        ScriptName = ScriptName
    };
}
