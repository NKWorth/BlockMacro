namespace MacroBlocks.Models.Actions;

public sealed class KeyPressBlock : ActionBlock
{
    private ushort _virtualKey;
    private string _keyLabel = "A";
    private bool _ctrl;
    private bool _alt;
    private bool _shift;

    /// <summary>
    /// Virtual-key code (Windows VK_*). Example: 0x41 = A.
    /// </summary>
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
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public bool Ctrl
    {
        get => _ctrl;
        set
        {
            if (SetField(ref _ctrl, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public bool Alt
    {
        get => _alt;
        set
        {
            if (SetField(ref _alt, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public bool Shift
    {
        get => _shift;
        set
        {
            if (SetField(ref _shift, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Key Press";

    public override string Summary
    {
        get
        {
            var mods = new List<string>();
            if (Ctrl)
            {
                mods.Add("Ctrl");
            }

            if (Alt)
            {
                mods.Add("Alt");
            }

            if (Shift)
            {
                mods.Add("Shift");
            }

            mods.Add(KeyLabel);
            return string.Join("+", mods);
        }
    }

    public override MacroBlock Clone() => new KeyPressBlock
    {
        VirtualKey = VirtualKey,
        KeyLabel = KeyLabel,
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift
    };
}
