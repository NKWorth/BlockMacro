using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BlockMacro.Models;
using BlockMacro.Services;

namespace BlockMacro.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly HashSet<ObservableCollection<MacroBlock>> _subscribedCollections;
    private readonly MacroPlaybackEngine _engine;
    private readonly IScriptLibrary _library;
    private readonly ScreenPointPicker _pointPicker = new();
    private MacroScript _script;
    private MacroBlock? _selectedBlock;
    private MacroScript? _selectedLibraryScript;
    private string _status = "Ready";
    private bool _isRunning;
    private bool _loopForever;
    private bool _isRecordingLocation;
    private WindowState _windowStateBeforeRecord = WindowState.Normal;
    private string _keyPressEventKeyText = "F";
    private string _scriptName = "Untitled Macro";

    public MainViewModel()
        : this(new JsonScriptLibrary())
    {
    }

    public MainViewModel(IScriptLibrary library)
        : this(new MacroPlaybackEngine(new Win32InputSimulator(), library), library)
    {
    }

    public MainViewModel(MacroPlaybackEngine engine, IScriptLibrary library)
    {
        _engine = engine;
        _library = library;
        _script = new MacroScript();
        _scriptName = _script.Name;
        AvailableEvents = [];
        LibraryScripts = [];
        _subscribedCollections = [];

        EnsureTreeSubscriptions(_script.Blocks);
        _library.Changed += (_, _) => Application.Current.Dispatcher.Invoke(RefreshLibrary);

        _engine.Started += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRunning = true;
                RefreshCommands();
            });
        };

        _engine.Stopped += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRunning = false;
                Status = "Stopped";
                RefreshCommands();
            });
        };

        _engine.StatusChanged += (_, message) =>
        {
            Application.Current.Dispatcher.Invoke(() => Status = message);
        };

        AddDelayCommand = new RelayCommand(AddDelay, CanEditScript);
        AddMouseClickCommand = new RelayCommand(AddMouseClick, CanEditScript);
        AddMouseMoveCommand = new RelayCommand(AddMouseMove, CanEditScript);
        AddKeyPressCommand = new RelayCommand(AddKeyPress, CanEditScript);
        AddKeyPressEventCommand = new RelayCommand(AddKeyPressEvent, CanEditScript);
        AddContinueUntilCommand = new RelayCommand(AddContinueUntil, CanEditScript);
        AddRunSubscriptCommand = new RelayCommand(AddRunSubscript, CanEditScript);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => CanEditScript() && SelectedBlock is not null);
        MoveUpCommand = new RelayCommand(MoveUp, () => CanEditScript() && CanMoveSelected(-1));
        MoveDownCommand = new RelayCommand(MoveDown, () => CanEditScript() && CanMoveSelected(1));
        ClearCommand = new RelayCommand(Clear, () => CanEditScript() && Blocks.Count > 0);
        RunCommand = new RelayCommand(async () => await RunAsync(), () => !IsRunning && !IsRecordingLocation && Blocks.Count > 0);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        RecordMouseMoveLocationCommand = new RelayCommand(
            async () => await RecordMouseMoveLocationAsync(),
            () => !IsRunning && !IsRecordingLocation && SelectedMouseMove is not null);
        CancelRecordLocationCommand = new RelayCommand(CancelRecordLocation, () => IsRecordingLocation);
        NewScriptCommand = new RelayCommand(NewScript, CanEditScript);
        SaveScriptCommand = new RelayCommand(SaveScript, () => CanEditScript() && Blocks.Count > 0);
        OpenLibraryScriptCommand = new RelayCommand(OpenLibraryScript, () => CanEditScript() && SelectedLibraryScript is not null);
        DeleteLibraryScriptCommand = new RelayCommand(DeleteLibraryScript, () => CanEditScript() && SelectedLibraryScript is not null);

        RefreshLibrary();
    }

    public MacroScript Script => _script;

    public ObservableCollection<MacroBlock> Blocks => _script.Blocks;

    public ObservableCollection<EventBlock> AvailableEvents { get; }

    public ObservableCollection<MacroScript> LibraryScripts { get; }

    public string ScriptName
    {
        get => _scriptName;
        set
        {
            if (SetProperty(ref _scriptName, value))
            {
                _script.Name = value;
            }
        }
    }

    public MacroBlock? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (_selectedBlock is INotifyPropertyChanged oldNpc)
            {
                oldNpc.PropertyChanged -= OnSelectedBlockPropertyChanged;
            }

            if (SetProperty(ref _selectedBlock, value))
            {
                if (_selectedBlock is INotifyPropertyChanged newNpc)
                {
                    newNpc.PropertyChanged += OnSelectedBlockPropertyChanged;
                }

                if (_selectedBlock is KeyPressEventBlock keyEvent)
                {
                    _keyPressEventKeyText = keyEvent.KeyLabel;
                    OnPropertyChanged(nameof(KeyPressEventKeyText));
                }

                NotifySelectionProperties();
                RefreshCommands();
            }
        }
    }

    public MacroScript? SelectedLibraryScript
    {
        get => _selectedLibraryScript;
        set
        {
            if (SetProperty(ref _selectedLibraryScript, value))
            {
                RefreshCommands();
            }
        }
    }

    public MouseMoveBlock? SelectedMouseMove => SelectedBlock as MouseMoveBlock;
    public bool HasMouseMoveSelection => SelectedMouseMove is not null;

    public KeyPressEventBlock? SelectedKeyPressEvent => SelectedBlock as KeyPressEventBlock;
    public bool HasKeyPressEventSelection => SelectedKeyPressEvent is not null;

    public ContinueUntilBlock? SelectedContinueUntil => SelectedBlock as ContinueUntilBlock;
    public bool HasContinueUntilSelection => SelectedContinueUntil is not null;

    public RunSubscriptBlock? SelectedRunSubscript => SelectedBlock as RunSubscriptBlock;
    public bool HasRunSubscriptSelection => SelectedRunSubscript is not null;

    public Guid? SelectedContinueUntilEventId
    {
        get => SelectedContinueUntil?.EventBlockId;
        set
        {
            if (SelectedContinueUntil is null)
            {
                return;
            }

            var evt = value is { } id
                ? AvailableEvents.FirstOrDefault(e => e.Id == id)
                : null;
            SelectContinueUntilEvent(evt);
            OnPropertyChanged();
        }
    }

    public Guid? SelectedRunSubscriptId
    {
        get => SelectedRunSubscript?.ScriptId;
        set
        {
            if (SelectedRunSubscript is null)
            {
                return;
            }

            var script = value is { } id
                ? LibraryScripts.FirstOrDefault(s => s.Id == id)
                : null;
            SelectRunSubscript(script);
            OnPropertyChanged();
        }
    }

    public string KeyPressEventKeyText
    {
        get => _keyPressEventKeyText;
        set
        {
            if (!SetProperty(ref _keyPressEventKeyText, value) || SelectedKeyPressEvent is null)
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().ToUpperInvariant();
            SelectedKeyPressEvent.KeyLabel = label;
            SelectedKeyPressEvent.VirtualKey = ResolveVirtualKey(label);
            SyncContinueUntilLabels();
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool IsRecordingLocation
    {
        get => _isRecordingLocation;
        private set
        {
            if (SetProperty(ref _isRecordingLocation, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool LoopForever
    {
        get => _loopForever;
        set
        {
            if (SetProperty(ref _loopForever, value))
            {
                _script.LoopForever = value;
            }
        }
    }

    public ICommand AddDelayCommand { get; }
    public ICommand AddMouseClickCommand { get; }
    public ICommand AddMouseMoveCommand { get; }
    public ICommand AddKeyPressCommand { get; }
    public ICommand AddKeyPressEventCommand { get; }
    public ICommand AddContinueUntilCommand { get; }
    public ICommand AddRunSubscriptCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RecordMouseMoveLocationCommand { get; }
    public ICommand CancelRecordLocationCommand { get; }
    public ICommand NewScriptCommand { get; }
    public ICommand SaveScriptCommand { get; }
    public ICommand OpenLibraryScriptCommand { get; }
    public ICommand DeleteLibraryScriptCommand { get; }

    private bool CanEditScript() => !IsRunning && !IsRecordingLocation;

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(SelectedMouseMove));
        OnPropertyChanged(nameof(HasMouseMoveSelection));
        OnPropertyChanged(nameof(SelectedKeyPressEvent));
        OnPropertyChanged(nameof(HasKeyPressEventSelection));
        OnPropertyChanged(nameof(SelectedContinueUntil));
        OnPropertyChanged(nameof(HasContinueUntilSelection));
        OnPropertyChanged(nameof(SelectedContinueUntilEventId));
        OnPropertyChanged(nameof(SelectedRunSubscript));
        OnPropertyChanged(nameof(HasRunSubscriptSelection));
        OnPropertyChanged(nameof(SelectedRunSubscriptId));
    }

    private void AddDelay()
    {
        var block = new DelayBlock { Milliseconds = 500 };
        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void AddMouseClick()
    {
        var block = new MouseClickBlock { X = 100, Y = 100, Button = Models.MouseButton.Left };
        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void AddMouseMove()
    {
        var block = new MouseMoveBlock { X = 200, Y = 200 };
        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void AddKeyPress()
    {
        var block = new KeyPressBlock
        {
            VirtualKey = 0x41,
            KeyLabel = "A"
        };
        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void AddKeyPressEvent()
    {
        var block = new KeyPressEventBlock();
        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void AddContinueUntil()
    {
        var block = new ContinueUntilBlock();

        if (AvailableEvents.Count > 0)
        {
            var evt = AvailableEvents[0];
            block.EventBlockId = evt.Id;
            block.EventLabel = evt.Name;
        }

        Blocks.Add(block);
        EnsureTreeSubscriptions(block.Children);
        SelectedBlock = block;
    }

    private void AddRunSubscript()
    {
        var block = new RunSubscriptBlock();
        var candidates = LibraryScripts.Where(s => s.Id != _script.Id).ToList();
        if (candidates.Count > 0)
        {
            block.ScriptId = candidates[0].Id;
            block.ScriptName = candidates[0].Name;
        }

        Blocks.Add(block);
        SelectedBlock = block;
    }

    private void RemoveSelected()
    {
        if (SelectedBlock is null)
        {
            return;
        }

        if (!BlockTree.TryFindOwner(Blocks, SelectedBlock, out var owner, out var index))
        {
            return;
        }

        owner.RemoveAt(index);
        SelectedBlock = owner.Count == 0
            ? null
            : owner[Math.Clamp(index, 0, owner.Count - 1)];
    }

    private bool CanMoveSelected(int delta)
    {
        if (SelectedBlock is null
            || !BlockTree.TryFindOwner(Blocks, SelectedBlock, out var owner, out var index))
        {
            return false;
        }

        var target = index + delta;
        return target >= 0 && target < owner.Count;
    }

    private void MoveUp() => MoveSelected(-1);

    private void MoveDown() => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (SelectedBlock is null
            || !BlockTree.TryFindOwner(Blocks, SelectedBlock, out var owner, out var index))
        {
            return;
        }

        var target = index + delta;
        if (target < 0 || target >= owner.Count)
        {
            return;
        }

        owner.Move(index, target);
        RefreshCommands();
    }

    private void Clear()
    {
        Blocks.Clear();
        SelectedBlock = null;
    }

    private void NewScript()
    {
        ReplaceWorkingScript(new MacroScript());
        Status = "New script";
    }

    private void SaveScript()
    {
        if (Blocks.Count == 0)
        {
            return;
        }

        _script.Name = string.IsNullOrWhiteSpace(ScriptName) ? "Untitled Macro" : ScriptName.Trim();
        ScriptName = _script.Name;
        _script.LoopForever = LoopForever;
        _library.Save(_script);
        RefreshLibrary();
        SelectedLibraryScript = LibraryScripts.FirstOrDefault(s => s.Id == _script.Id);
        SyncRunSubscriptLabels();
        Status = $"Saved '{_script.Name}' to library";
    }

    private void OpenLibraryScript()
    {
        if (SelectedLibraryScript is null)
        {
            return;
        }

        var loaded = _library.Get(SelectedLibraryScript.Id);
        if (loaded is null)
        {
            Status = "Could not open script from library";
            RefreshLibrary();
            return;
        }

        ReplaceWorkingScript(loaded);
        Status = $"Opened '{loaded.Name}'";
    }

    private void DeleteLibraryScript()
    {
        if (SelectedLibraryScript is null)
        {
            return;
        }

        var id = SelectedLibraryScript.Id;
        var name = SelectedLibraryScript.Name;
        if (!_library.Delete(id))
        {
            Status = "Could not delete library script";
            return;
        }

        if (_script.Id == id)
        {
            // Keep editing a copy, but assign a new id so the next save creates a new entry.
            _script.Id = Guid.NewGuid();
        }

        RefreshLibrary();
        SyncRunSubscriptLabels();
        Status = $"Deleted '{name}' from library";
    }

    private void ReplaceWorkingScript(MacroScript script)
    {
        foreach (var collection in _subscribedCollections.ToList())
        {
            collection.CollectionChanged -= OnBlocksChanged;
        }

        _subscribedCollections.Clear();
        _script = script;
        EnsureTreeSubscriptions(_script.Blocks);

        _scriptName = _script.Name;
        _loopForever = _script.LoopForever;
        SelectedBlock = null;

        OnPropertyChanged(nameof(Script));
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(ScriptName));
        OnPropertyChanged(nameof(LoopForever));

        RefreshAvailableEvents();
        SyncContinueUntilLabels();
        SyncRunSubscriptLabels();
        RefreshCommands();
    }

    private void RefreshLibrary()
    {
        var selectedId = SelectedLibraryScript?.Id;
        LibraryScripts.Clear();
        foreach (var script in _library.List())
        {
            LibraryScripts.Add(script);
        }

        SelectedLibraryScript = selectedId is { } id
            ? LibraryScripts.FirstOrDefault(s => s.Id == id)
            : null;

        SyncRunSubscriptLabels();
        OnPropertyChanged(nameof(SelectedRunSubscriptId));
    }

    private async Task RunAsync()
    {
        if (Blocks.Count == 0)
        {
            return;
        }

        _script.LoopForever = LoopForever;
        _script.Name = ScriptName;
        Status = "Starting…";

        try
        {
            await _engine.RunAsync(_script);
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            IsRunning = false;
            RefreshCommands();
        }
    }

    private void Stop()
    {
        _engine.Stop();
        Status = "Stopping…";
    }

    private async Task RecordMouseMoveLocationAsync()
    {
        if (SelectedMouseMove is null || IsRecordingLocation)
        {
            return;
        }

        var target = SelectedMouseMove;
        var window = Application.Current.MainWindow;
        IsRecordingLocation = true;
        Status = "Click anywhere to set the mouse location (Esc to cancel)…";

        if (window is not null)
        {
            _windowStateBeforeRecord = window.WindowState;
            window.WindowState = WindowState.Minimized;
        }

        try
        {
            var point = await _pointPicker.PickNextClickAsync();
            if (point is { } captured && ReferenceEquals(SelectedMouseMove, target))
            {
                target.X = captured.X;
                target.Y = captured.Y;
                Status = $"Location set to ({captured.X}, {captured.Y})";
            }
            else if (point is null)
            {
                Status = "Location recording cancelled";
            }
        }
        catch (Exception ex)
        {
            Status = $"Record failed: {ex.Message}";
        }
        finally
        {
            IsRecordingLocation = false;
            if (window is not null)
            {
                window.WindowState = _windowStateBeforeRecord == WindowState.Minimized
                    ? WindowState.Normal
                    : _windowStateBeforeRecord;
                window.Activate();
            }
        }
    }

    private void CancelRecordLocation()
    {
        _pointPicker.Cancel();
    }

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureTreeSubscriptions(Blocks);
        RefreshAvailableEvents();
        SyncContinueUntilLabels();
        SyncRunSubscriptLabels();
    }

    private void EnsureTreeSubscriptions(ObservableCollection<MacroBlock> list)
    {
        if (_subscribedCollections.Add(list))
        {
            list.CollectionChanged += OnBlocksChanged;
        }

        foreach (var flow in list.OfType<ContinueUntilBlock>())
        {
            EnsureTreeSubscriptions(flow.Children);
        }
    }

    private void OnSelectedBlockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is EventBlock && e.PropertyName is nameof(EventBlock.Name) or nameof(KeyPressEventBlock.KeyLabel))
        {
            SyncContinueUntilLabels();
        }
    }

    private void RefreshAvailableEvents()
    {
        AvailableEvents.Clear();
        foreach (var evt in BlockTree.EnumerateEvents(Blocks))
        {
            AvailableEvents.Add(evt);
        }

        OnPropertyChanged(nameof(AvailableEvents));
    }

    private void SyncContinueUntilLabels()
    {
        var events = BlockTree.EnumerateEvents(Blocks).ToDictionary(e => e.Id);
        foreach (var cont in BlockTree.Enumerate(Blocks).OfType<ContinueUntilBlock>())
        {
            if (cont.EventBlockId is { } id && events.TryGetValue(id, out var evt))
            {
                cont.EventLabel = evt.Name;
            }
            else if (cont.EventBlockId is not null)
            {
                cont.EventLabel = "(missing event)";
            }
        }
    }

    private void SyncRunSubscriptLabels()
    {
        var library = LibraryScripts.ToDictionary(s => s.Id);
        foreach (var call in BlockTree.EnumerateSubscripts(Blocks))
        {
            if (call.ScriptId is { } id && library.TryGetValue(id, out var script))
            {
                call.ScriptName = script.Name;
            }
            else if (call.ScriptId is not null)
            {
                call.ScriptName = "(missing script)";
            }
        }
    }

    private void SelectContinueUntilEvent(EventBlock? evt)
    {
        if (SelectedContinueUntil is null)
        {
            return;
        }

        AssignEventToContinueUntil(SelectedContinueUntil, evt);
    }

    public void MoveBlockInto(
        MacroBlock block,
        ObservableCollection<MacroBlock> targetOwner,
        int targetIndex)
    {
        if (!CanEditScript()
            || !BlockTree.TryFindOwner(Blocks, block, out var sourceOwner, out var sourceIndex))
        {
            return;
        }

        if (block is ContinueUntilBlock flow && BlockTree.IsOwnedBy(targetOwner, flow))
        {
            Status = "Cannot move a flow into itself";
            return;
        }

        if (ReferenceEquals(sourceOwner, targetOwner))
        {
            var to = targetIndex;
            if (sourceIndex < to)
            {
                to--;
            }

            to = Math.Clamp(to, 0, targetOwner.Count - 1);
            if (sourceIndex != to)
            {
                targetOwner.Move(sourceIndex, to);
            }
        }
        else
        {
            sourceOwner.RemoveAt(sourceIndex);
            var insertAt = Math.Clamp(targetIndex, 0, targetOwner.Count);
            targetOwner.Insert(insertAt, block);
            if (block is ContinueUntilBlock nested)
            {
                EnsureTreeSubscriptions(nested.Children);
            }
        }

        SelectedBlock = block;
        Status = "Moved block";
    }

    public void MoveBlockRelative(
        MacroBlock block,
        MacroBlock relativeTo,
        bool insertAfter)
    {
        if (!CanEditScript()
            || !BlockTree.TryFindOwner(Blocks, relativeTo, out var targetOwner, out var relativeIndex))
        {
            return;
        }

        var targetIndex = insertAfter ? relativeIndex + 1 : relativeIndex;
        MoveBlockInto(block, targetOwner, targetIndex);
    }

    public void InsertPaletteBlock(string kind, ObservableCollection<MacroBlock> owner, int index)
    {
        if (!CanEditScript())
        {
            return;
        }

        var block = CreatePaletteBlock(kind);
        if (block is null)
        {
            return;
        }

        var insertAt = Math.Clamp(index, 0, owner.Count);
        owner.Insert(insertAt, block);
        if (block is ContinueUntilBlock flow)
        {
            EnsureTreeSubscriptions(flow.Children);
            if (AvailableEvents.Count > 0)
            {
                var evt = AvailableEvents[0];
                flow.EventBlockId = evt.Id;
                flow.EventLabel = evt.Name;
            }
        }

        SelectedBlock = block;
        Status = $"Added {block.DisplayName}";
    }

    public static MacroBlock? CreatePaletteBlock(string kind)
        => kind switch
        {
            "Delay" => new DelayBlock { Milliseconds = 500 },
            "MouseMove" => new MouseMoveBlock { X = 200, Y = 200 },
            "MouseClick" => new MouseClickBlock { X = 100, Y = 100, Button = Models.MouseButton.Left },
            "KeyPress" => new KeyPressBlock { VirtualKey = 0x41, KeyLabel = "A" },
            "ContinueUntil" => new ContinueUntilBlock(),
            "RunSubscript" => new RunSubscriptBlock(),
            "KeyPressEvent" => new KeyPressEventBlock(),
            _ => null
        };

    public static (string Title, string Subtitle) DescribePaletteKind(string kind)
        => kind switch
        {
            "Delay" => ("Delay", "500 ms"),
            "MouseMove" => ("Mouse Move", "(200, 200) · instant"),
            "MouseClick" => ("Mouse Click", "Left @ (100, 100)"),
            "KeyPress" => ("Key Press", "A"),
            "ContinueUntil" => ("Continue Until", "until (no event)"),
            "RunSubscript" => ("Run Subscript", "(no script)"),
            "KeyPressEvent" => ("Event: Press Key", "Press F"),
            _ => (kind, string.Empty)
        };

    public void AssignEventToContinueUntil(ContinueUntilBlock flow, EventBlock? evt)
    {
        if (!CanEditScript())
        {
            return;
        }

        if (evt is null)
        {
            flow.EventBlockId = null;
            flow.EventLabel = "(no event)";
            Status = "Cleared Continue Until event";
            return;
        }

        flow.EventBlockId = evt.Id;
        flow.EventLabel = evt.Name;
        SelectedBlock = flow;
        Status = $"Assigned '{evt.Name}' to Continue Until";
    }

    /// <summary>
    /// Creates a Press Key event from the palette and assigns it to a Continue Until block.
    /// </summary>
    public void DropPaletteKeyPressEventOnto(ContinueUntilBlock flow)
    {
        if (!CanEditScript())
        {
            return;
        }

        var evt = new KeyPressEventBlock();
        if (BlockTree.TryFindOwner(Blocks, flow, out var owner, out var index))
        {
            owner.Insert(index, evt);
        }
        else
        {
            Blocks.Add(evt);
        }

        AssignEventToContinueUntil(flow, evt);
        Status = $"Created and assigned '{evt.Name}' to Continue Until";
    }

    private void SelectRunSubscript(MacroScript? script)
    {
        if (SelectedRunSubscript is null)
        {
            return;
        }

        if (script is null)
        {
            SelectedRunSubscript.ScriptId = null;
            SelectedRunSubscript.ScriptName = "(no script)";
            return;
        }

        SelectedRunSubscript.ScriptId = script.Id;
        SelectedRunSubscript.ScriptName = script.Name;
    }

    private static ushort ResolveVirtualKey(string label)
    {
        if (label.Length == 1)
        {
            var ch = char.ToUpperInvariant(label[0]);
            if (ch is >= 'A' and <= 'Z')
            {
                return ch;
            }

            if (ch is >= '0' and <= '9')
            {
                return ch;
            }
        }

        return label.ToUpperInvariant() switch
        {
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "TAB" => 0x09,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0x46
        };
    }

    private void RefreshCommands()
    {
        (AddDelayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddMouseClickCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddMouseMoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddKeyPressCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddKeyPressEventCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddContinueUntilCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddRunSubscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RunCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RecordMouseMoveLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelRecordLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NewScriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveScriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenLibraryScriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteLibraryScriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
