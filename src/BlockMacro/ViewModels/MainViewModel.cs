using System.Collections.ObjectModel;
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

    public MainViewModel()
        : this(new MacroPlaybackEngine(new Win32InputSimulator()))
    {
    }

    public MainViewModel(MacroPlaybackEngine engine)
    {
        _engine = engine;
        Script = new MacroScript();

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

        AddDelayCommand = new RelayCommand(AddDelay, () => !IsRunning && !IsRecordingLocation);
        AddMouseClickCommand = new RelayCommand(AddMouseClick, () => !IsRunning && !IsRecordingLocation);
        AddMouseMoveCommand = new RelayCommand(AddMouseMove, () => !IsRunning && !IsRecordingLocation);
        AddKeyPressCommand = new RelayCommand(AddKeyPress, () => !IsRunning && !IsRecordingLocation);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => !IsRunning && !IsRecordingLocation && SelectedBlock is not null);
        MoveUpCommand = new RelayCommand(MoveUp, () => !IsRunning && !IsRecordingLocation && CanMoveSelected(-1));
        MoveDownCommand = new RelayCommand(MoveDown, () => !IsRunning && !IsRecordingLocation && CanMoveSelected(1));
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && !IsRecordingLocation && Blocks.Count > 0);
        RunCommand = new RelayCommand(async () => await RunAsync(), () => !IsRunning && !IsRecordingLocation && Blocks.Count > 0);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        RecordMouseMoveLocationCommand = new RelayCommand(
            async () => await RecordMouseMoveLocationAsync(),
            () => !IsRunning && !IsRecordingLocation && SelectedMouseMove is not null);
        CancelRecordLocationCommand = new RelayCommand(CancelRecordLocation, () => IsRecordingLocation);
    }

    public MacroScript Script { get; }

    public ObservableCollection<MacroBlock> Blocks => Script.Blocks;

    public MacroBlock? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (SetProperty(ref _selectedBlock, value))
            {
                OnPropertyChanged(nameof(SelectedMouseMove));
                OnPropertyChanged(nameof(HasMouseMoveSelection));
                RefreshCommands();
            }
        }
    }

    public MouseMoveBlock? SelectedMouseMove => SelectedBlock as MouseMoveBlock;

    public bool HasMouseMoveSelection => SelectedMouseMove is not null;

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
    public ICommand RemoveSelectedCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RecordMouseMoveLocationCommand { get; }
    public ICommand CancelRecordLocationCommand { get; }

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

    private void RemoveSelected()
    {
        if (SelectedBlock is null)
        {
            return;
        }

        var index = Blocks.IndexOf(SelectedBlock);
        Blocks.Remove(SelectedBlock);
        SelectedBlock = Blocks.Count == 0
            ? null
            : Blocks[Math.Clamp(index, 0, Blocks.Count - 1)];
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

    private void RefreshCommands()
    {
        (AddDelayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddMouseClickCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddMouseMoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddKeyPressCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
