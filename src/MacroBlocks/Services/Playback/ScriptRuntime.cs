namespace MacroBlocks.Services.Playback;

/// <summary>
/// Per-run state for event flags and Boolean outputs used by branching.
/// </summary>
public sealed class ScriptRuntime
{
    private readonly Dictionary<Guid, bool> _triggered = [];
    private readonly Dictionary<Guid, bool> _nodeBooleanOutputs = [];

    /// <summary>Boolean output of the script currently being executed (Return Boolean).</summary>
    public bool CurrentBooleanOutput { get; private set; }

    public void BeginScriptOutput() => CurrentBooleanOutput = false;

    public void SetBooleanOutput(bool value) => CurrentBooleanOutput = value;

    public void RememberNodeOutput(Guid nodeId, bool value) => _nodeBooleanOutputs[nodeId] = value;

    public bool GetNodeOutput(Guid nodeId)
        => _nodeBooleanOutputs.TryGetValue(nodeId, out var value) && value;

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
