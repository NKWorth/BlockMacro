# BlockMacro

Windows desktop app for arranging mouse and keyboard actions as reusable blocks, then running (or endlessly looping) the resulting script with one-click start/stop.

## Stack

- **C# / .NET 10** + **WPF**
- Win32 `SendInput` for mouse/keyboard injection
- MVVM playback engine with cancellation for instant Stop

## Run

```powershell
dotnet run --project src\BlockMacro\BlockMacro.csproj
```

## Layout

```
src/BlockMacro/
  Models/          # Delay, MouseMove, MouseClick, KeyPress, MacroScript
  Services/        # IInputSimulator, Win32InputSimulator, MacroPlaybackEngine
  ViewModels/      # MainViewModel + commands
  Native/          # P/Invoke for user32 SendInput
  MainWindow.xaml  # Block palette + script list + Run/Stop
```

## Next steps

- Property inspector for editing selected blocks
- Record mode (capture live mouse/keyboard into blocks)
- Global hotkey for Stop
- Save / load scripts as JSON
