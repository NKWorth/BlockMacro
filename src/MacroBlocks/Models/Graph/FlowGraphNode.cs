using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MacroBlocks.Models.Graph;

/// <summary>
/// A node on the flow graph (run a library script, or branch with If).
/// </summary>
public sealed class FlowGraphNode : INotifyPropertyChanged
{
    private FlowGraphNodeKind _kind = FlowGraphNodeKind.RunScript;
    private Guid? _scriptId;
    private string _label = "Script";
    private double _x;
    private double _y;

    public Guid Id { get; set; } = Guid.NewGuid();

    public FlowGraphNodeKind Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    /// <summary>Library script id when <see cref="Kind"/> is <see cref="FlowGraphNodeKind.RunScript"/>.</summary>
    public Guid? ScriptId
    {
        get => _scriptId;
        set => SetField(ref _scriptId, value);
    }

    /// <summary>Cached display name (script name or "If").</summary>
    public string Label
    {
        get => _label;
        set
        {
            if (SetField(ref _label, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    [JsonIgnore]
    public string DisplayTitle => Kind == FlowGraphNodeKind.If ? "If" : Label;

    public FlowGraphNode Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        ScriptId = ScriptId,
        Label = Label,
        X = X,
        Y = Y
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
