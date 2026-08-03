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
    private readonly MacroPlaybackEngine _engine;
    private readonly ScreenPointPicker _pointPicker = new();
    private MacroBlock? _selectedBlock;
    private string _status = "Ready";
    private bool _isRunning;
    private bool _loopForever;
    private bool _isRecordingLocation;
    private WindowState _windowStateBeforeRecord = WindowState.Normal;
    private string _keyPressEventKeyText = "F";

    public MainViewModel()
        : this(new MacroPlaybackEngine(new Win32InputSimulator()))
    {
    }

    public MainViewModel(MacroPlaybackEngine engine)
    {
        _engine = engine;
        Script = new MacroScript();
        AvailableEvents = [];

        Script.Blocks.CollectionChanged += OnBlocksChanged;

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
    }

    public MacroScript Script { get; }

    public ObservableCollection<MacroBlock> Blocks => Script.Blocks;

    public ObservableCollection<EventBlock> AvailableEvents { get; }

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

                OnPropertyChanged(nameof(SelectedMouseMove));
                OnPropertyChanged(nameof(HasMouseMoveSelection));
                OnPropertyChanged(nameof(SelectedKeyPressEvent));
                OnPropertyChanged(nameof(HasKeyPressEventSelection));
                OnPropertyChanged(nameof(SelectedContinueUntil));
                OnPropertyChanged(nameof(HasContinueUntilSelection));
                OnPropertyChanged(nameof(SelectedContinueUntilEventId));
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
                Script.LoopForever = value;
            }
        }
    }

    public ICommand AddDelayCommand { get; }
    public ICommand AddMouseClickCommand { get; }
    public ICommand AddMouseMoveCommand { get; }
    public ICommand AddKeyPressCommand { get; }
    public ICommand AddKeyPressEventCommand { get; }
    public ICommand AddContinueUntilCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RecordMouseMoveLocationCommand { get; }
    public ICommand CancelRecordLocationCommand { get; }

    private bool CanEditScript() => !IsRunning && !IsRecordingLocation;

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
        var end = new EndContinueBlock();

        if (AvailableEvents.Count > 0)
        {
            var evt = AvailableEvents[0];
            block.EventBlockId = evt.Id;
            block.EventLabel = evt.Name;
        }

        Blocks.Add(block);
        Blocks.Add(end);
        SelectedBlock = block;
    }

    private void RemoveSelected()
    {
        if (SelectedBlock is null)
        {
            return;
        }

        var index = Blocks.IndexOf(SelectedBlock);

        if (SelectedBlock is ContinueUntilBlock)
        {
            var endIndex = FindMatchingEndContinue(index);
            if (endIndex > index)
            {
                Blocks.RemoveAt(endIndex);
            }
        }

        Blocks.Remove(SelectedBlock);
        SelectedBlock = Blocks.Count == 0
            ? null
            : Blocks[Math.Clamp(index, 0, Blocks.Count - 1)];
    }

    private int FindMatchingEndContinue(int continueIndex)
    {
        var depth = 0;
        for (var i = continueIndex + 1; i < Blocks.Count; i++)
        {
            switch (Blocks[i])
            {
                case ContinueUntilBlock:
                    depth++;
                    break;
                case EndContinueBlock when depth == 0:
                    return i;
                case EndContinueBlock:
                    depth--;
                    break;
            }
        }

        return -1;
    }

    private bool CanMoveSelected(int delta)
    {
        if (SelectedBlock is null)
        {
            return false;
        }

        var index = Blocks.IndexOf(SelectedBlock);
        var target = index + delta;
        return index >= 0 && target >= 0 && target < Blocks.Count;
    }

    private void MoveUp() => MoveSelected(-1);

    private void MoveDown() => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (SelectedBlock is null)
        {
            return;
        }

        var index = Blocks.IndexOf(SelectedBlock);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Blocks.Count)
        {
            return;
        }

        Blocks.Move(index, target);
        RefreshCommands();
    }

    private void Clear()
    {
        Blocks.Clear();
        SelectedBlock = null;
    }

    private async Task RunAsync()
    {
        if (Blocks.Count == 0)
        {
            return;
        }

        Script.LoopForever = LoopForever;
        Status = "Starting…";

        try
        {
            await _engine.RunAsync(Script);
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
        RefreshAvailableEvents();
        SyncContinueUntilLabels();
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
        foreach (var evt in Blocks.OfType<EventBlock>())
        {
            AvailableEvents.Add(evt);
        }

        OnPropertyChanged(nameof(AvailableEvents));
    }

    private void SyncContinueUntilLabels()
    {
        var events = Blocks.OfType<EventBlock>().ToDictionary(e => e.Id);
        foreach (var cont in Blocks.OfType<ContinueUntilBlock>())
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

    /// <summary>
    /// Called from the Continue Until inspector when the user picks an event.
    /// </summary>
    public void SelectContinueUntilEvent(EventBlock? evt)
    {
        if (SelectedContinueUntil is null)
        {
            return;
        }

        if (evt is null)
        {
            SelectedContinueUntil.EventBlockId = null;
            SelectedContinueUntil.EventLabel = "(no event)";
            return;
        }

        SelectedContinueUntil.EventBlockId = evt.Id;
        SelectedContinueUntil.EventLabel = evt.Name;
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
        (RemoveSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RunCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RecordMouseMoveLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelRecordLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
