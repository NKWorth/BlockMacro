# MacroBlocks

Windows desktop app for arranging mouse and keyboard actions as reusable blocks, then running (or endlessly looping) the resulting script with one-click start/stop.

## Stack

- **C# / .NET 10** + **WPF**
- Win32 `SendInput` for mouse/keyboard injection
- MVVM playback engine with cancellation for instant Stop
- Saved script library with nested flow, events, and subscripts

## Run

```powershell
dotnet run --project src\MacroBlocks\MacroBlocks.csproj
```

## Architecture

```
src/MacroBlocks/
  Models/
    Actions/       # ActionBlock + Delay, MouseMove, MouseClick, KeyPress, ReturnBoolean
    Events/        # EventBlock + KeyPressEvent
    Flow/          # FlowBlock + ContinueUntil, RunSubscript, EndContinue
    Graph/         # FlowGraph orchestration (RunScript / If nodes + edges)
  Services/
    Playback/      # engine, runtime, history, event watcher, graph walker
    Input/         # IInputSimulator, Win32 SendInput, point picker
    Persistence/   # script library + JSON
  ViewModels/      # MainViewModel, FlowGraphViewModel + commands
  Ui/
    Drag/          # ghost, formats, insertion gaps
    Converters/    # WPF value converters
  Native/          # P/Invoke
  MainWindow.xaml  # shell view (Script | Flow Graph)
```

Domain hierarchy: `MacroBlock` → `ActionBlock` | `EventBlock` | `FlowBlock`.  
Containers use `IBlockContainer` / `IEventSlotHost`; walk via `BlockTree`.  
Orchestration: `MacroScript.FlowGraph` wires library scripts and If branches; `ReturnBoolean` sets subscript Boolean outputs.

See [AGENTS.md](AGENTS.md) for growth conventions.
