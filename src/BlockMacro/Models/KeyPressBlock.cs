namespace BlockMacro.Models;

public sealed class KeyPressBlock : MacroBlock
{
    /// <summary>
    /// Virtual-key code (Windows VK_*). Example: 0x41 = A.
    /// </summary>
    public ushort VirtualKey { get; set; }

    public string KeyLabel { get; set; } = "A";

    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }

    public override string DisplayName => "Key Press";

    public override string Summary
    {
        get
        {
            var mods = new List<string>();
            if (Ctrl) mods.Add("Ctrl");
            if (Alt) mods.Add("Alt");
            if (Shift) mods.Add("Shift");
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
