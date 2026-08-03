namespace MacroBlocks.Models.Graph;

/// <summary>
/// Which output/input port an edge connects through.
/// </summary>
public enum FlowGraphPort
{
    /// <summary>Sequential next after a RunScript (or fall-through).</summary>
    Next,

    /// <summary>Boolean condition into an If node.</summary>
    Condition,

    /// <summary>Taken when the If condition is true.</summary>
    Then,

    /// <summary>Taken when the If condition is false.</summary>
    Else
}
