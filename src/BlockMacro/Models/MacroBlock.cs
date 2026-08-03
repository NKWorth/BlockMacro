using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlockMacro.Models;

/// <summary>
/// Base type for every arrangeable macro step.
/// </summary>
public abstract class MacroBlock : INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();

    public abstract string DisplayName { get; }

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
