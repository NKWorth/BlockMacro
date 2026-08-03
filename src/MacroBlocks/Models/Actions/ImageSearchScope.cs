namespace MacroBlocks.Models.Actions;

/// <summary>
/// Where to search when looking for a template image on screen.
/// </summary>
public enum ImageSearchScope
{
    /// <summary>Entire virtual desktop (all monitors).</summary>
    FullVirtualScreen,

    /// <summary>Primary monitor only.</summary>
    PrimaryMonitor,

    /// <summary>Explicit rectangle in virtual-screen coordinates.</summary>
    ScreenRegion,

    /// <summary>Entire client area of a matching application window (resolved at run time).</summary>
    ApplicationWindow,

    /// <summary>Sub-rectangle of a matching application window's client area.</summary>
    ApplicationWindowRegion
}
