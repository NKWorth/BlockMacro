namespace MacroBlocks.Models.Events;

/// <summary>
/// Declarative block that can raise a runtime flag while a script is running.
/// Event blocks do not perform actions when reached in sequence — they are armed for the whole run.
/// </summary>
public abstract class EventBlock : MacroBlock
{
    private string _name = "Event";

    /// <summary>
    /// User-facing label used by Continue Until and the inspector.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public override string DisplayName => $"Event: {EventKind}";

    public abstract string EventKind { get; }

    public override string Summary => Name;
}
