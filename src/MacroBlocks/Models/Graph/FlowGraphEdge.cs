namespace MacroBlocks.Models.Graph;

/// <summary>
/// Directed edge between graph nodes through a named port.
/// </summary>
public sealed class FlowGraphEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromId { get; set; }

    public Guid ToId { get; set; }

    public FlowGraphPort Port { get; set; } = FlowGraphPort.Next;

    public FlowGraphEdge Clone() => new()
    {
        Id = Id,
        FromId = FromId,
        ToId = ToId,
        Port = Port
    };
}
