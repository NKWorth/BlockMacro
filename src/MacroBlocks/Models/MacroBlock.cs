using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MacroBlocks.Models;

/// <summary>
/// Base type for every arrangeable macro step.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DelayBlock), "delay")]
[JsonDerivedType(typeof(MouseMoveBlock), "mouseMove")]
[JsonDerivedType(typeof(MouseClickBlock), "mouseClick")]
[JsonDerivedType(typeof(KeyPressBlock), "keyPress")]
[JsonDerivedType(typeof(KeyPressEventBlock), "eventKeyPress")]
[JsonDerivedType(typeof(ContinueUntilBlock), "continueUntil")]
[JsonDerivedType(typeof(EndContinueBlock), "endContinue")]
[JsonDerivedType(typeof(RunSubscriptBlock), "runSubscript")]
public abstract class MacroBlock : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public abstract string DisplayName { get; }

    [JsonIgnore]
    public abstract string Summary { get; }

    public abstract MacroBlock Clone();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
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
