using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MacroBlocks.Models.Graph;

namespace MacroBlocks.Models;

/// <summary>
/// Ordered sequence of blocks that can be played, saved, and reused as a subscript.
/// Optionally owns a <see cref="FlowGraph"/> for orchestrating library scripts.
/// </summary>
public sealed class MacroScript
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Macro";

    public bool LoopForever { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public ObservableCollection<MacroBlock> Blocks { get; } = [];

    /// <summary>
    /// Serialization surface for <see cref="Blocks"/>.
    /// Legacy flat End Continue markers are nested on load.
    /// </summary>
    [JsonPropertyName("blocks")]
    public List<MacroBlock> BlocksForStorage
    {
        get => Blocks.ToList();
        set
        {
            Blocks.Clear();
            if (value is null)
            {
                return;
            }

            foreach (var block in ScriptMigrator.ToNested(value))
            {
                Blocks.Add(block);
            }
        }
    }

    /// <summary>
    /// Optional orchestration graph. When non-empty, Run prefers the graph over <see cref="Blocks"/>.
    /// </summary>
    public FlowGraph FlowGraph { get; set; } = new();

    public MacroScript CloneDeep()
    {
        var copy = new MacroScript
        {
            Id = Id,
            Name = Name,
            LoopForever = LoopForever,
            UpdatedAt = UpdatedAt,
            FlowGraph = FlowGraph.CloneDeep()
        };

        foreach (var block in Blocks)
        {
            var cloned = block.Clone();
            cloned.Id = block.Id;
            copy.Blocks.Add(cloned);
        }

        return copy;
    }
}
