namespace BlockMacro.Models;

/// <summary>
/// Loops the blocks between this marker and the matching <see cref="EndContinueBlock"/>
/// until the referenced event flag is raised.
/// </summary>
public sealed class ContinueUntilBlock : MacroBlock
{
    private Guid? _eventBlockId;
    private string _eventLabel = "(no event)";

    public Guid? EventBlockId
    {
        get => _eventBlockId;
        set
        {
            if (SetField(ref _eventBlockId, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string EventLabel
    {
        get => _eventLabel;
        set
        {
            if (SetField(ref _eventLabel, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Continue Until";

    public override string Summary => $"until {EventLabel}";

    public override MacroBlock Clone() => new ContinueUntilBlock
    {
        EventBlockId = EventBlockId,
        EventLabel = EventLabel
    };
}
