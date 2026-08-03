using MacroBlocks.Models;

namespace MacroBlocks.Services.Playback;

public sealed class MacroPlaybackEngine
{
    private readonly IInputSimulator _input;
    private readonly IScriptLibrary _library;
    private CancellationTokenSource? _cts;
    private Task? _running;

    public MacroPlaybackEngine(IInputSimulator input, IScriptLibrary library)
    {
        _input = input;
        _library = library;
    }

    public bool IsRunning => _running is { IsCompleted: false };

    public event EventHandler? Started;
    public event EventHandler? Stopped;
    public event EventHandler<string>? StatusChanged;

    public async Task RunAsync(MacroScript script, CancellationToken externalToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A macro is already running.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = _cts.Token;

        _running = Task.Run(async () =>
        {
            var runtime = new ScriptRuntime();
            KeyPressEventWatcher? watcher = null;

            try
            {
                var reachable = CollectReachableScripts(script).ToList();
                var allEvents = reachable.SelectMany(s => BlockTree.EnumerateEvents(s.Blocks)).ToList();
                foreach (var evt in allEvents)
                {
                    runtime.RegisterEvent(evt.Id);
                }

                var keyEvents = allEvents.OfType<KeyPressEventBlock>().ToList();
                watcher = new KeyPressEventWatcher(runtime, keyEvents);
                watcher.Start();

                Started?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, "Running");

                var callStack = new Stack<Guid>();
                callStack.Push(script.Id);

                do
                {
                    foreach (var evt in allEvents)
                    {
                        runtime.Reset(evt.Id);
                    }

                    var blocks = script.Blocks.ToArray();
                    await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, callStack, token)
                        .ConfigureAwait(false);
                }
                while (script.LoopForever && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                // Expected on Stop.
            }
            finally
            {
                watcher?.Dispose();
                StatusChanged?.Invoke(this, "Stopped");
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }, token);

        await _running.ConfigureAwait(false);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private IEnumerable<MacroScript> CollectReachableScripts(MacroScript root)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<MacroScript>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            yield return current;

            foreach (var call in BlockTree.EnumerateSubscripts(current.Blocks))
            {
                if (call.ScriptId is not { } id)
                {
                    continue;
                }

                var sub = _library.Get(id);
                if (sub is not null)
                {
                    queue.Enqueue(sub);
                }
            }
        }
    }

    private async Task ExecuteRangeAsync(
        IReadOnlyList<MacroBlock> blocks,
        int start,
        int end,
        ScriptRuntime runtime,
        Stack<Guid> callStack,
        CancellationToken token)
    {
        for (var i = start; i < end; i++)
        {
            token.ThrowIfCancellationRequested();
            var block = blocks[i];

            switch (block)
            {
                case EventBlock:
                case EndContinueBlock:
                    break;

                case ContinueUntilBlock continueUntil:
                    await ExecuteContinueUntilAsync(continueUntil, runtime, callStack, token)
                        .ConfigureAwait(false);
                    break;

                case RunSubscriptBlock call:
                    StatusChanged?.Invoke(this, $"Running: {call.DisplayName} — {call.Summary}");
                    await ExecuteSubscriptAsync(call, runtime, callStack, token).ConfigureAwait(false);
                    break;

                default:
                    StatusChanged?.Invoke(this, $"Running: {block.DisplayName} — {block.Summary}");
                    await ExecuteActionAsync(block, token).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task ExecuteSubscriptAsync(
        RunSubscriptBlock call,
        ScriptRuntime runtime,
        Stack<Guid> callStack,
        CancellationToken token)
    {
        if (call.ScriptId is not { } scriptId)
        {
            throw new InvalidOperationException("Run Subscript has no script selected.");
        }

        if (callStack.Contains(scriptId))
        {
            throw new InvalidOperationException(
                $"Circular subscript reference detected while calling '{call.ScriptName}'.");
        }

        var sub = _library.Get(scriptId)
            ?? throw new InvalidOperationException($"Subscript '{call.ScriptName}' was not found in the library.");

        callStack.Push(scriptId);
        try
        {
            var blocks = sub.Blocks.ToArray();
            await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, callStack, token)
                .ConfigureAwait(false);
        }
        finally
        {
            callStack.Pop();
        }
    }

    private async Task ExecuteContinueUntilAsync(
        ContinueUntilBlock continueUntil,
        ScriptRuntime runtime,
        Stack<Guid> callStack,
        CancellationToken token)
    {
        if (continueUntil.EventSlot is null || continueUntil.EventBlockId is not { } eventId)
        {
            throw new InvalidOperationException("Continue Until has no event in its event slot.");
        }

        runtime.Reset(eventId);
        StatusChanged?.Invoke(this, $"Continue until {continueUntil.EventLabel}…");

        while (!runtime.IsTriggered(eventId))
        {
            token.ThrowIfCancellationRequested();

            if (continueUntil.Children.Count == 0)
            {
                await Task.Delay(25, token).ConfigureAwait(false);
                continue;
            }

            var body = continueUntil.Children.ToArray();
            await ExecuteRangeAsync(body, 0, body.Length, runtime, callStack, token)
                .ConfigureAwait(false);

            if (!runtime.IsTriggered(eventId))
            {
                await Task.Delay(1, token).ConfigureAwait(false);
            }
        }

        StatusChanged?.Invoke(this, $"Event reached: {continueUntil.EventLabel}");
    }

    private async Task ExecuteActionAsync(MacroBlock block, CancellationToken token)
    {
        switch (block)
        {
            case DelayBlock delay:
                await Task.Delay(Math.Max(0, delay.Milliseconds), token).ConfigureAwait(false);
                break;

            case MouseMoveBlock move:
                await _input.MoveMouseAsync(move.X, move.Y, move.DurationMilliseconds, token)
                    .ConfigureAwait(false);
                break;

            case MouseClickBlock click:
                _input.Click(click.X, click.Y, click.Button, click.ClickCount);
                break;

            case KeyPressBlock key:
                _input.KeyPress(key.VirtualKey, key.Ctrl, key.Alt, key.Shift);
                break;

            default:
                throw new NotSupportedException($"Unknown block type: {block.GetType().Name}");
        }
    }
}
