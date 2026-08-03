using System.Collections.ObjectModel;

namespace MacroBlocks.Models;

/// <summary>
/// A block that owns a nested body of child blocks (control-flow containers).
/// </summary>
public interface IBlockContainer
{
    ObservableCollection<MacroBlock> Children { get; }
}
