# MacroBlocks agent guide

## Architecture

Keep a layered structure. New code belongs in the matching layer and namespace:

| Layer | Path | Namespace | Responsibility |
|-------|------|-----------|----------------|
| Domain | `Models/` | `MacroBlocks.Models*` | Script/block data, tree helpers, migration |
| Application / infra | `Services/` | `MacroBlocks.Services.*` | Playback, input injection, persistence |
| Presentation logic | `ViewModels/` | `MacroBlocks.ViewModels` | Commands, selection, inspector state |
| UI chrome | `Ui/` | `MacroBlocks.Ui*` | Drag/drop helpers, converters, templates |
| Native | `Native/` | `MacroBlocks.Native` | P/Invoke only |

### Domain block hierarchy

```
MacroBlock
├── ActionBlock     Models/Actions/     Delay, MouseMove, MouseClick, KeyPress
├── EventBlock      Models/Events/      KeyPressEvent (and future events)
└── FlowBlock       Models/Flow/        ContinueUntil, RunSubscript, EndContinue
```

- Prefer extending the matching base (`ActionBlock` / `EventBlock` / `FlowBlock`).
- Nested bodies implement `IBlockContainer`; event slots implement `IEventSlotHost`.
- Walk the tree via `BlockTree` + those interfaces — do not special-case every new container in UI code.
- Keep JSON type discriminators stable when renaming files/namespaces.

### Services

```
Services/
  Playback/      MacroPlaybackEngine, ScriptRuntime, watchers, ScriptHistory
  Input/         IInputSimulator, Win32InputSimulator, ScreenPointPicker
  Persistence/   IScriptLibrary, JsonScriptLibrary, ScriptJson
```

- Depend on interfaces at boundaries (`IInputSimulator`, `IScriptLibrary`).
- Playback must not reference WPF types.

### UI

```
Ui/
  Drag/          ghost, formats, insertion gaps
  Converters/    WPF value converters
  BlockTemplateSelector.cs
```

- Keep `MainWindow.xaml(.cs)` thin: wire events to the view model; put reusable chrome under `Ui/`.
- ViewModels stay free of drag-visuals and Win32 details.

## Growth rules

1. **One concern per folder** — do not dump new types into project root.
2. **Namespace matches folder** under `Models`, `Services`, and `Ui`.
3. **No circular deps** — Models ← Services ← ViewModels/Ui; Native only used by Input.
4. **New block types** — add under Actions/Events/Flow, register on `MacroBlock` polymorphic attributes, update palette + inspector only as needed.
5. **Prefer small focused types** over growing `MainViewModel` / `MainWindow` further; extract collaborators when a feature adds a distinct responsibility.
6. **Tests** (when added) mirror layers: domain tests for blocks/tree, service tests for playback/persistence.

## Run

```powershell
dotnet run --project src\MacroBlocks\MacroBlocks.csproj
```
