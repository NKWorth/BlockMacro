using System.Text.Json.Serialization;

namespace MacroBlocks.Models.Actions;

/// <summary>
/// Searches the screen (or a window/region) for a template image and sets the
/// script Boolean output based on whether the best match meets <see cref="Confidence"/>.
/// </summary>
public sealed class FindImageBlock : ActionBlock
{
    private string _imageFileName = string.Empty;
    private double _confidence = 0.8;
    private ImageSearchScope _scope = ImageSearchScope.FullVirtualScreen;
    private int _regionX;
    private int _regionY;
    private int _regionWidth = 200;
    private int _regionHeight = 200;
    private string _windowTitleContains = string.Empty;
    private string _windowProcessName = string.Empty;

    /// <summary>
    /// File name under %LocalAppData%/MacroBlocks/images (not a full path).
    /// </summary>
    public string ImageFileName
    {
        get => _imageFileName;
        set
        {
            if (SetField(ref _imageFileName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageFileName);

    /// <summary>
    /// Minimum match score in [0, 1]. Lower values tolerate transparency / slight differences.
    /// </summary>
    public double Confidence
    {
        get => _confidence;
        set
        {
            var clamped = Math.Clamp(value, 0.05, 1.0);
            if (SetField(ref _confidence, clamped))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public ImageSearchScope Scope
    {
        get => _scope;
        set
        {
            if (SetField(ref _scope, value))
            {
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(UsesWindow));
                OnPropertyChanged(nameof(UsesRegion));
            }
        }
    }

    [JsonIgnore]
    public bool UsesWindow =>
        Scope is ImageSearchScope.ApplicationWindow or ImageSearchScope.ApplicationWindowRegion;

    [JsonIgnore]
    public bool UsesRegion =>
        Scope is ImageSearchScope.ScreenRegion or ImageSearchScope.ApplicationWindowRegion;

    public int RegionX
    {
        get => _regionX;
        set => SetField(ref _regionX, value);
    }

    public int RegionY
    {
        get => _regionY;
        set => SetField(ref _regionY, value);
    }

    public int RegionWidth
    {
        get => _regionWidth;
        set => SetField(ref _regionWidth, Math.Max(1, value));
    }

    public int RegionHeight
    {
        get => _regionHeight;
        set => SetField(ref _regionHeight, Math.Max(1, value));
    }

    /// <summary>Case-insensitive substring match against the window title.</summary>
    public string WindowTitleContains
    {
        get => _windowTitleContains;
        set
        {
            if (SetField(ref _windowTitleContains, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    /// <summary>Optional process name without .exe (e.g. "notepad").</summary>
    public string WindowProcessName
    {
        get => _windowProcessName;
        set
        {
            if (SetField(ref _windowProcessName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public override string DisplayName => "Find Image";

    public override string Summary
    {
        get
        {
            var img = HasImage ? ImageFileName : "(no image)";
            var conf = $"{Confidence:P0}";
            return Scope switch
            {
                ImageSearchScope.FullVirtualScreen => $"{img} ≥ {conf} · all screens",
                ImageSearchScope.PrimaryMonitor => $"{img} ≥ {conf} · primary",
                ImageSearchScope.ScreenRegion => $"{img} ≥ {conf} · region",
                ImageSearchScope.ApplicationWindow => $"{img} ≥ {conf} · window",
                ImageSearchScope.ApplicationWindowRegion => $"{img} ≥ {conf} · window region",
                _ => $"{img} ≥ {conf}"
            };
        }
    }

    public override MacroBlock Clone() => new FindImageBlock
    {
        ImageFileName = ImageFileName,
        Confidence = Confidence,
        Scope = Scope,
        RegionX = RegionX,
        RegionY = RegionY,
        RegionWidth = RegionWidth,
        RegionHeight = RegionHeight,
        WindowTitleContains = WindowTitleContains,
        WindowProcessName = WindowProcessName
    };
}
