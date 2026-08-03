using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MacroBlocks.Models;
using MacroBlocks.Services;

namespace MacroBlocks.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly HashSet<ObservableCollection<MacroBlock>> _subscribedCollections;
    private readonly HashSet<MacroBlock> _trackedBlocks = [];
    private readonly ScriptHistory _history = new();
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
    private string _keyPressKeyText = "A";
    private string _scriptName = "Untitled Macro";
    private string? _selectedPaletteKind;

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
        _history.Attach(() => _script);
        _history.Reset(_script);
        _history.Changed += (_, _) => RefreshCommands();
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

        SelectPaletteCommand = new RelayCommand(
            p => SelectPaletteKind(p as string),
            _ => CanEditScript());
        InsertPaletteDraftCommand = new RelayCommand(InsertPaletteDraft, () => CanEditScript() && IsPaletteDraft);
        RemoveSelectedCommand = new RelayCommand(
            RemoveSelected,
            () => CanEditScript() && SelectedBlock is not null && BlockTree.TryLocate(Blocks, SelectedBlock, out _));
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
        UndoCommand = new RelayCommand(Undo, () => CanEditScript() && _history.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanEditScript() && _history.CanRedo);

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
            if (_scriptName == value)
            {
                return;
            }

            _history.OnPropertyEdited(_script);
            _scriptName = value;
            _script.Name = value;
            OnPropertyChanged();
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
                if (value is not null && BlockTree.TryLocate(Blocks, value, out _))
                {
                    SelectedPaletteKind = null;
                }

                if (_selectedBlock is INotifyPropertyChanged newNpc)
                {
                    newNpc.PropertyChanged += OnSelectedBlockPropertyChanged;
                }

                if (_selectedBlock is KeyPressEventBlock keyEvent)
                {
                    _keyPressEventKeyText = keyEvent.KeyLabel;
                    OnPropertyChanged(nameof(KeyPressEventKeyText));
                }

                if (_selectedBlock is KeyPressBlock keyPress)
                {
                    _keyPressKeyText = keyPress.KeyLabel;
                    OnPropertyChanged(nameof(KeyPressKeyText));
                }

                NotifySelectionProperties();
                using (_history.ApplyScope())
                {
                    RefreshAvailableEvents();
                }

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

    public MouseClickBlock? SelectedMouseClick => SelectedBlock as MouseClickBlock;
    public bool HasMouseClickSelection => SelectedMouseClick is not null;

    public DelayBlock? SelectedDelay => SelectedBlock as DelayBlock;
    public bool HasDelaySelection => SelectedDelay is not null;

    public KeyPressBlock? SelectedKeyPress => SelectedBlock as KeyPressBlock;
    public bool HasKeyPressSelection => SelectedKeyPress is not null;

    public KeyPressEventBlock? SelectedKeyPressEvent => SelectedBlock as KeyPressEventBlock;
    public bool HasKeyPressEventSelection => SelectedKeyPressEvent is not null;

    public ContinueUntilBlock? SelectedContinueUntil => SelectedBlock as ContinueUntilBlock;
    public bool HasContinueUntilSelection => SelectedContinueUntil is not null;

    public RunSubscriptBlock? SelectedRunSubscript => SelectedBlock as RunSubscriptBlock;
    public bool HasRunSubscriptSelection => SelectedRunSubscript is not null;

    public string? SelectedPaletteKind
    {
        get => _selectedPaletteKind;
        private set
        {
            if (SetProperty(ref _selectedPaletteKind, value))
            {
                OnPropertyChanged(nameof(IsPaletteDraft));
                RefreshCommands();
            }
        }
    }

    /// <summary>
    /// True when the inspector is editing a palette draft that is not yet in the script.
    /// </summary>
    public bool IsPaletteDraft =>
        SelectedPaletteKind is not null
        && SelectedBlock is not null
        && !BlockTree.TryLocate(Blocks, SelectedBlock, out _);

    public Array MouseButtonOptions { get; } = Enum.GetValues(typeof(MacroBlocks.Models.Actions.MouseButton));

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
            using (_history.ApplyScope())
            {
                SyncContinueUntilLabels();
            }
        }
    }

    public string KeyPressKeyText
    {
        get => _keyPressKeyText;
        set
        {
            if (!SetProperty(ref _keyPressKeyText, value) || SelectedKeyPress is null)
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().ToUpperInvariant();
            SelectedKeyPress.KeyLabel = label;
            SelectedKeyPress.VirtualKey = ResolveVirtualKey(label);
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
            if (_loopForever == value)
            {
                return;
            }

            _history.CheckpointBeforeChange(_script);
            _loopForever = value;
            _script.LoopForever = value;
            OnPropertyChanged();
            _history.CaptureBaseline(_script);
        }
    }

    public ICommand SelectPaletteCommand { get; }
    public ICommand InsertPaletteDraftCommand { get; }
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
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }

    private bool CanEditScript() => !IsRunning && !IsRecordingLocation;

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(SelectedMouseMove));
        OnPropertyChanged(nameof(HasMouseMoveSelection));
        OnPropertyChanged(nameof(SelectedMouseClick));
        OnPropertyChanged(nameof(HasMouseClickSelection));
        OnPropertyChanged(nameof(SelectedDelay));
        OnPropertyChanged(nameof(HasDelaySelection));
        OnPropertyChanged(nameof(SelectedKeyPress));
        OnPropertyChanged(nameof(HasKeyPressSelection));
        OnPropertyChanged(nameof(SelectedKeyPressEvent));
        OnPropertyChanged(nameof(HasKeyPressEventSelection));
        OnPropertyChanged(nameof(SelectedContinueUntil));
        OnPropertyChanged(nameof(HasContinueUntilSelection));
        OnPropertyChanged(nameof(SelectedContinueUntilEventId));
        OnPropertyChanged(nameof(SelectedRunSubscript));
        OnPropertyChanged(nameof(HasRunSubscriptSelection));
        OnPropertyChanged(nameof(SelectedRunSubscriptId));
        OnPropertyChanged(nameof(IsPaletteDraft));
    }

    private void Mutate(Action action)
    {
        _history.CheckpointBeforeChange(_script);
        action();
        _history.CaptureBaseline(_script);
        RefreshCommands();
    }

    public void SelectPaletteKind(string? kind)
    {
        if (!CanEditScript() || string.IsNullOrWhiteSpace(kind))
        {
            return;
        }

        if (SelectedPaletteKind == kind && IsPaletteDraft)
        {
            Status = $"Configure {SelectedBlock!.DisplayName}, then Insert or drag into the script";
            return;
        }

        var draft = CreateConfiguredPaletteBlock(kind);
        if (draft is null)
        {
            return;
        }

        SelectedPaletteKind = kind;
        SelectedBlock = draft;
        Status = $"Configure {draft.DisplayName}, then Insert or drag into the script";
    }

    private void InsertPaletteDraft()
    {
        if (!CanEditScript() || !IsPaletteDraft || SelectedBlock is null || SelectedPaletteKind is null)
        {
            return;
        }

        var clone = CloneForInsert(SelectedBlock);
        Mutate(() =>
        {
            Blocks.Add(clone);
            if (clone is ContinueUntilBlock flow)
            {
                EnsureTreeSubscriptions(flow.Children);
                if (flow.EventSlot is not null)
                {
                    TrackBlock(flow.EventSlot);
                }
            }

            SelectedPaletteKind = null;
            SelectedBlock = clone;
            Status = $"Inserted {clone.DisplayName}";
        });
    }

    private MacroBlock? CreateConfiguredPaletteBlock(string kind)
    {
        var block = CreatePaletteBlock(kind);
        if (block is RunSubscriptBlock run)
        {
            var candidates = LibraryScripts.Where(s => s.Id != _script.Id).ToList();
            if (candidates.Count > 0)
            {
                run.ScriptId = candidates[0].Id;
                run.ScriptName = candidates[0].Name;
            }
        }

        return block;
    }

    private static MacroBlock CloneForInsert(MacroBlock source)
    {
        var clone = source.Clone();
        // Fresh identity for the script copy; keep nested child ids from Clone when present.
        clone.Id = Guid.NewGuid();
        if (clone is ContinueUntilBlock flow && flow.EventSlot is not null)
        {
            flow.EventSlot.Id = Guid.NewGuid();
        }

        return clone;
    }

    private void RemoveSelected()
    {
        if (SelectedBlock is null || !BlockTree.TryLocate(Blocks, SelectedBlock, out var location))
        {
            return;
        }

        Mutate(() =>
        {
            switch (location)
            {
                case EventSlotLocation slot:
                    slot.Flow.EventSlot = null;
                    SelectedBlock = slot.Flow;
                    Status = "Removed event from Continue Until";
                    break;

                case CollectionLocation col:
                    col.Owner.RemoveAt(col.Index);
                    SelectedBlock = col.Owner.Count == 0
                        ? null
                        : col.Owner[Math.Clamp(col.Index, 0, col.Owner.Count - 1)];
                    break;
            }
        });
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

        Mutate(() => owner.Move(index, target));
    }

    private void Clear() => Mutate(() =>
    {
        Blocks.Clear();
        SelectedBlock = null;
    });

    private void NewScript()
    {
        ReplaceWorkingScript(new MacroScript(), resetHistory: true);
        Status = "New script";
    }

    private void Undo()
    {
        var snapshot = _history.Undo(_script);
        if (snapshot is null)
        {
            return;
        }

        using (_history.ApplyScope())
        {
            ReplaceWorkingScript(snapshot, resetHistory: false);
        }

        Status = "Undo";
        RefreshCommands();
    }

    private void Redo()
    {
        var snapshot = _history.Redo(_script);
        if (snapshot is null)
        {
            return;
        }

        using (_history.ApplyScope())
        {
            ReplaceWorkingScript(snapshot, resetHistory: false);
        }

        Status = "Redo";
        RefreshCommands();
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

        ReplaceWorkingScript(loaded, resetHistory: true);
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

    private void ReplaceWorkingScript(MacroScript script, bool resetHistory = true)
    {
        foreach (var collection in _subscribedCollections.ToList())
        {
            collection.CollectionChanged -= OnBlocksChanged;
        }

        foreach (var block in _trackedBlocks.ToList())
        {
            block.PropertyChanged -= OnTrackedBlockPropertyChanged;
        }

        _subscribedCollections.Clear();
        _trackedBlocks.Clear();
        _script = script;
        EnsureTreeSubscriptions(_script.Blocks);

        _scriptName = _script.Name;
        _loopForever = _script.LoopForever;
        SelectedPaletteKind = null;
        SelectedBlock = null;

        OnPropertyChanged(nameof(Script));
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(ScriptName));
        OnPropertyChanged(nameof(LoopForever));

        using (_history.ApplyScope())
        {
            RefreshAvailableEvents();
            SyncContinueUntilLabels();
            SyncRunSubscriptLabels();
        }

        if (resetHistory)
        {
            _history.Reset(_script);
        }
        else
        {
            _history.CaptureBaseline(_script);
        }

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
                var inScript = BlockTree.TryLocate(Blocks, target, out _);
                if (inScript)
                {
                    _history.CheckpointBeforeChange(_script);
                }

                target.X = captured.X;
                target.Y = captured.Y;

                if (inScript)
                {
                    _history.CaptureBaseline(_script);
                }

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

    private void EnsureTreeSubscriptions(ObservableCollection<MacroBlock> list)
    {
        if (_subscribedCollections.Add(list))
        {
            list.CollectionChanged += OnBlocksChanged;
        }

        foreach (var block in list)
        {
            TrackBlock(block);
            if (block is ContinueUntilBlock flow)
            {
                if (flow.EventSlot is not null)
                {
                    TrackBlock(flow.EventSlot);
                }

                EnsureTreeSubscriptions(flow.Children);
            }
        }
    }

    private void TrackBlock(MacroBlock block)
    {
        if (!_trackedBlocks.Add(block))
        {
            return;
        }

        block.PropertyChanged += OnTrackedBlockPropertyChanged;
    }

    private void OnTrackedBlockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MacroBlock.Summary)
            or nameof(MacroBlock.DisplayName)
            or nameof(ContinueUntilBlock.EventLabel)
            or nameof(RunSubscriptBlock.ScriptName))
        {
            return;
        }

        _history.OnPropertyEdited(_script);
        RefreshCommands();
    }

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureTreeSubscriptions(Blocks);
        using (_history.ApplyScope())
        {
            RefreshAvailableEvents();
            SyncContinueUntilLabels();
            SyncRunSubscriptLabels();
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
        foreach (var evt in BlockTree.EnumerateFreeEvents(Blocks))
        {
            AvailableEvents.Add(evt);
        }

        if (SelectedContinueUntil?.EventSlot is { } slotted
            && AvailableEvents.All(e => e.Id != slotted.Id))
        {
            AvailableEvents.Insert(0, slotted);
        }

        OnPropertyChanged(nameof(AvailableEvents));
    }

    private void SyncContinueUntilLabels()
    {
        foreach (var cont in BlockTree.Enumerate(Blocks).OfType<ContinueUntilBlock>())
        {
            cont.EventLabel = cont.EventSlot?.Name ?? "(no event)";
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
        if (!CanEditScript() || !BlockTree.TryLocate(Blocks, block, out var location))
        {
            return;
        }

        if (block is ContinueUntilBlock flow && BlockTree.IsOwnedBy(targetOwner, flow))
        {
            Status = "Cannot move a flow into itself";
            return;
        }

        if (block is EventBlock && IsEventBodyCollection(targetOwner))
        {
            Status = "Events belong in a Continue Until event slot, not the body";
            return;
        }

        Mutate(() =>
        {
            if (!BlockTree.TryLocate(Blocks, block, out location))
            {
                return;
            }

            DetachBlock(location);

            var insertAt = Math.Clamp(targetIndex, 0, targetOwner.Count);
            if (location is CollectionLocation col
                && ReferenceEquals(col.Owner, targetOwner)
                && col.Index < insertAt)
            {
                insertAt = Math.Clamp(insertAt - 1, 0, targetOwner.Count);
            }

            targetOwner.Insert(insertAt, block);
            if (block is ContinueUntilBlock nested)
            {
                EnsureTreeSubscriptions(nested.Children);
                if (nested.EventSlot is not null)
                {
                    TrackBlock(nested.EventSlot);
                }
            }

            SelectedBlock = block;
            Status = "Moved block";
        });
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

        if (kind is "KeyPressEvent" && IsEventBodyCollection(owner))
        {
            Status = "Drop events onto a Continue Until event slot";
            return;
        }

        var block = IsPaletteDraft && SelectedPaletteKind == kind && SelectedBlock is not null
            ? CloneForInsert(SelectedBlock)
            : CreateConfiguredPaletteBlock(kind);
        if (block is null)
        {
            return;
        }

        Mutate(() =>
        {
            var insertAt = Math.Clamp(index, 0, owner.Count);
            owner.Insert(insertAt, block);
            if (block is ContinueUntilBlock flow)
            {
                EnsureTreeSubscriptions(flow.Children);
                if (flow.EventSlot is not null)
                {
                    TrackBlock(flow.EventSlot);
                }
            }

            SelectedBlock = block;
            Status = $"Added {block.DisplayName}";
        });
    }

    public static MacroBlock? CreatePaletteBlock(string kind)
        => kind switch
        {
            "Delay" => new DelayBlock { Milliseconds = 500 },
            "MouseMove" => new MouseMoveBlock { X = 200, Y = 200 },
            "MouseClick" => new MouseClickBlock { X = 100, Y = 100, Button = MacroBlocks.Models.Actions.MouseButton.Left },
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

    /// <summary>
    /// Places an existing event into a Continue Until event slot (ejecting any previous occupant).
    /// </summary>
    public void PlaceEventInSlot(ContinueUntilBlock flow, EventBlock? evt)
    {
        if (!CanEditScript())
        {
            return;
        }

        Mutate(() =>
        {
            if (evt is null)
            {
                EjectEventSlot(flow);
                Status = "Cleared Continue Until event slot";
                return;
            }

            if (ReferenceEquals(flow.EventSlot, evt))
            {
                SelectedBlock = evt;
                return;
            }

            // Detach from wherever it currently lives.
            if (BlockTree.TryLocate(Blocks, evt, out var location))
            {
                DetachBlock(location);
            }

            EjectEventSlot(flow);
            flow.EventSlot = evt;
            TrackBlock(evt);
            SelectedBlock = evt;
            Status = $"Placed '{evt.Name}' in Continue Until event slot";
        });
    }

    public void AssignEventToContinueUntil(ContinueUntilBlock flow, EventBlock? evt)
        => PlaceEventInSlot(flow, evt);

    /// <summary>
    /// Creates a Press Key event from the palette and places it in the flow's event slot.
    /// </summary>
    public void DropPaletteKeyPressEventOnto(ContinueUntilBlock flow)
    {
        if (!CanEditScript())
        {
            return;
        }

        Mutate(() =>
        {
            var evt = IsPaletteDraft && SelectedBlock is KeyPressEventBlock draft
                ? (KeyPressEventBlock)CloneForInsert(draft)
                : new KeyPressEventBlock();
            EjectEventSlot(flow);
            flow.EventSlot = evt;
            TrackBlock(evt);
            SelectedBlock = evt;
            Status = $"Created '{evt.Name}' in Continue Until event slot";
        });
    }

    private void EjectEventSlot(ContinueUntilBlock flow)
    {
        if (flow.EventSlot is null)
        {
            return;
        }

        var previous = flow.EventSlot;
        flow.EventSlot = null;

        if (BlockTree.TryFindOwner(Blocks, flow, out var owner, out var index))
        {
            owner.Insert(index, previous);
        }
        else
        {
            Blocks.Add(previous);
        }

        TrackBlock(previous);
    }

    private static void DetachBlock(BlockLocation location)
    {
        switch (location)
        {
            case CollectionLocation col:
                col.Owner.RemoveAt(col.Index);
                break;
            case EventSlotLocation slot:
                slot.Flow.EventSlot = null;
                break;
        }
    }

    private bool IsEventBodyCollection(ObservableCollection<MacroBlock> owner)
    {
        foreach (var flow in BlockTree.Enumerate(Blocks).OfType<ContinueUntilBlock>())
        {
            if (ReferenceEquals(flow.Children, owner))
            {
                return true;
            }
        }

        return false;
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
        (SelectPaletteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (InsertPaletteDraftCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
