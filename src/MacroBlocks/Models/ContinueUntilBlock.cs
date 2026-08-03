using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MacroBlocks.Models;

/// <summary>
/// Loops nested body blocks until the slotted event flag is raised.
/// </summary>
public sealed class ContinueUntilBlock : MacroBlock
{
    private EventBlock? _eventSlot;
    private string _eventLabel = "(no event)";
    private Guid? _legacyEventBlockId;

    /// <summary>
    /// Event owned by this flow's event slot (not part of the body).
    /// </summary>
    [JsonIgnore]
    public EventBlock? EventSlot
    {
        get => _eventSlot;
        set
        {
            if (ReferenceEquals(_eventSlot, value))
            {
                return;
            }

            _eventSlot = value;
            EventLabel = value?.Name ?? "(no event)";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEventSlot));
            OnPropertyChanged(nameof(EventBlockId));
            OnPropertyChanged(nameof(Summary));
        }
    }

    [JsonIgnore]
    public bool HasEventSlot => EventSlot is not null;

    [JsonPropertyName("eventSlot")]
    public EventBlock? EventSlotForStorage
    {
        get => EventSlot;
        set => EventSlot = value;
    }

    /// <summary>
    /// Id of the slotted event, used by playback.
    /// </summary>
    [JsonIgnore]
    public Guid? EventBlockId => EventSlot?.Id;

    /// <summary>
    /// Legacy scripts stored a Guid reference to an event elsewhere in the tree.
    /// </summary>
    [JsonPropertyName("eventBlockId")]
    public Guid? EventBlockIdForStorage
    {
        get => null;
        set => _legacyEventBlockId = value;
    }

    internal Guid? LegacyEventBlockId => _legacyEventBlockId;

    internal void ClearLegacyEventBlockId() => _legacyEventBlockId = null;

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

    [JsonIgnore]
    public ObservableCollection<MacroBlock> Children { get; } = [];

    [JsonPropertyName("children")]
    public List<MacroBlock> ChildrenForStorage
    {
        get => Children.ToList();
        set
        {
            Children.Clear();
            if (value is null)
            {
                return;
            }

            foreach (var child in value)
            {
                Children.Add(child);
            }
        }
    }

    public override string DisplayName => "Continue Until";

    public override string Summary => $"until {EventLabel}";

    public override MacroBlock Clone()
    {
        var copy = new ContinueUntilBlock
        {
            EventLabel = EventLabel
        };

        if (EventSlot is not null)
        {
            var slotted = EventSlot.Clone();
            slotted.Id = EventSlot.Id;
            copy.EventSlot = (EventBlock)slotted;
        }

        foreach (var child in Children)
        {
            var cloned = child.Clone();
            cloned.Id = child.Id;
            copy.Children.Add(cloned);
        }

        return copy;
    }
}
