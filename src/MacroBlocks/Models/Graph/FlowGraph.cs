using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MacroBlocks.Models.Graph;

/// <summary>
/// Orchestration graph that wires library scripts and If branches.
/// </summary>
public sealed class FlowGraph
{
    [JsonIgnore]
    public ObservableCollection<FlowGraphNode> Nodes { get; } = [];

    [JsonIgnore]
    public ObservableCollection<FlowGraphEdge> Edges { get; } = [];

    [JsonPropertyName("nodes")]
    public List<FlowGraphNode> NodesForStorage
    {
        get => Nodes.ToList();
        set
        {
            Nodes.Clear();
            if (value is null)
            {
                return;
            }

            foreach (var node in value)
            {
                Nodes.Add(node);
            }
        }
    }

    [JsonPropertyName("edges")]
    public List<FlowGraphEdge> EdgesForStorage
    {
        get => Edges.ToList();
        set
        {
            Edges.Clear();
            if (value is null)
            {
                return;
            }

            foreach (var edge in value)
            {
                Edges.Add(edge);
            }
        }
    }

    [JsonIgnore]
    public bool HasNodes => Nodes.Count > 0;

    /// <summary>
    /// Entry = RunScript/If with no inbound Next/Then/Else edge.
    /// </summary>
    public FlowGraphNode? FindEntryNode()
    {
        var inbound = Edges
            .Where(e => e.Port is FlowGraphPort.Next or FlowGraphPort.Then or FlowGraphPort.Else)
            .Select(e => e.ToId)
            .ToHashSet();

        return Nodes.FirstOrDefault(n => !inbound.Contains(n.Id))
               ?? Nodes.FirstOrDefault();
    }

    public FlowGraphEdge? FindOutbound(Guid fromId, FlowGraphPort port)
        => Edges.FirstOrDefault(e => e.FromId == fromId && e.Port == port);

    public FlowGraphEdge? FindInboundCondition(Guid ifNodeId)
        => Edges.FirstOrDefault(e => e.ToId == ifNodeId && e.Port == FlowGraphPort.Condition);

    public FlowGraph CloneDeep()
    {
        var copy = new FlowGraph();
        foreach (var node in Nodes)
        {
            copy.Nodes.Add(node.Clone());
        }

        foreach (var edge in Edges)
        {
            copy.Edges.Add(edge.Clone());
        }

        return copy;
    }
}
