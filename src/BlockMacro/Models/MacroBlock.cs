namespace BlockMacro.Models;

/// <summary>
/// Base type for every arrangeable macro step.
/// </summary>
public abstract class MacroBlock
{
    public Guid Id { get; } = Guid.NewGuid();

    public abstract string DisplayName { get; }

    public abstract string Summary { get; }

    public abstract MacroBlock Clone();
}
