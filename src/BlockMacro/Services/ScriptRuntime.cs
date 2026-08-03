namespace BlockMacro.Services;

/// <summary>
/// Per-run state for event flags used by branching blocks.
/// </summary>
public sealed class ScriptRuntime
{
    private readonly Dictionary<Guid, bool> _triggered = [];

    public void RegisterEvent(Guid eventId) => _triggered[eventId] = false;

    public void Reset(Guid eventId)
    {
        if (_triggered.ContainsKey(eventId))
        {
            _triggered[eventId] = false;
        }
    }

    public void Trigger(Guid eventId)
    {
        if (_triggered.ContainsKey(eventId))
        {
            _triggered[eventId] = true;
        }
    }

    public bool IsTriggered(Guid eventId)
        => _triggered.TryGetValue(eventId, out var value) && value;
}
