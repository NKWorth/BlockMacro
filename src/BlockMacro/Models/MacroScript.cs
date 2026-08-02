using System.Collections.ObjectModel;

namespace BlockMacro.Models;

/// <summary>
/// Ordered sequence of blocks that can be played once or endlessly.
/// </summary>
public sealed class MacroScript
{
    public string Name { get; set; } = "Untitled Macro";

    public ObservableCollection<MacroBlock> Blocks { get; } = [];

    public bool LoopForever { get; set; }
}
