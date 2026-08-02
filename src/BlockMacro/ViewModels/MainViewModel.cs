using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BlockMacro.Models;
using BlockMacro.Services;

namespace BlockMacro.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly MacroPlaybackEngine _engine;
    private MacroBlock? _selectedBlock;
    private string _status = "Ready";
    private bool _isRunning;
    private bool _loopForever;

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

        AddDelayCommand = new RelayCommand(AddDelay, () => !IsRunning);
        AddMouseClickCommand = new RelayCommand(AddMouseClick, () => !IsRunning);
        AddMouseMoveCommand = new RelayCommand(AddMouseMove, () => !IsRunning);
        AddKeyPressCommand = new RelayCommand(AddKeyPress, () => !IsRunning);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => !IsRunning && SelectedBlock is not null);
        MoveUpCommand = new RelayCommand(MoveUp, () => !IsRunning && CanMoveSelected(-1));
        MoveDownCommand = new RelayCommand(MoveDown, () => !IsRunning && CanMoveSelected(1));
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && Blocks.Count > 0);
        RunCommand = new RelayCommand(async () => await RunAsync(), () => !IsRunning && Blocks.Count > 0);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
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
                RefreshCommands();
            }
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
    }
}
