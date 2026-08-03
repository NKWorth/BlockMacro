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

## Layout

```
src/MacroBlocks/
  Models/
    Actions/       # Delay, MouseMove, MouseClick, KeyPress (+ ActionBlock)
    Events/        # EventBlock, KeyPressEventBlock
    Flow/          # ContinueUntil, RunSubscript, EndContinue (+ FlowBlock)
    MacroBlock, MacroScript, BlockTree, ScriptMigrator
  Services/        # Input simulator, playback engine, script library
  ViewModels/      # MainViewModel + commands
  Native/          # P/Invoke for user32 SendInput
  MainWindow.xaml  # Block palette + script list + Run/Stop
```
