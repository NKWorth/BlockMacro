namespace MacroBlocks.Models;

/// <summary>
/// Flips a runtime flag when the user manually presses the configured key during playback.
/// Simulated (injected) key presses from the script itself are ignored.
/// </summary>
public sealed class KeyPressEventBlock : EventBlock
{
    private ushort _virtualKey = 0x46; // F
    private string _keyLabel = "F";

    public KeyPressEventBlock()
    {
        Name = "Press F";
    }

    public ushort VirtualKey
    {
        get => _virtualKey;
        set => SetField(ref _virtualKey, value);
    }

    public string KeyLabel
    {
        get => _keyLabel;
        set
        {
            if (SetField(ref _keyLabel, value))
            {
                if (string.IsNullOrWhiteSpace(Name) || Name.StartsWith("Press ", StringComparison.Ordinal))
                {
                    Name = $"Press {value}";
                }

                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string EventKind => "Press Key";

    public override string Summary => $"{Name} ({KeyLabel})";

    public override MacroBlock Clone() => new KeyPressEventBlock
    {
        Name = Name,
        VirtualKey = VirtualKey,
        KeyLabel = KeyLabel
    };
}
