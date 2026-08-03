using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MacroBlocks.Models;

/// <summary>
/// Loops nested body blocks until the referenced event flag is raised.
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
            EventBlockId = EventBlockId,
            EventLabel = EventLabel
        };

        foreach (var child in Children)
        {
            var cloned = child.Clone();
            cloned.Id = child.Id;
            copy.Children.Add(cloned);
        }

        return copy;
    }
}
