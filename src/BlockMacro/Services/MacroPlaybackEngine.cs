using BlockMacro.Models;

namespace BlockMacro.Services;

public sealed class MacroPlaybackEngine
{
    private readonly IInputSimulator _input;
    private CancellationTokenSource? _cts;
    private Task? _running;

    public MacroPlaybackEngine(IInputSimulator input)
    {
        _input = input;
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
                var blocks = script.Blocks.ToArray();
                ArmEvents(blocks, runtime);

                watcher = new KeyPressEventWatcher(runtime, blocks.OfType<KeyPressEventBlock>());
                watcher.Start();

                Started?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, "Running");

                do
                {
                    // Fresh event flags each outer loop iteration when Loop Forever is on.
                    foreach (var evt in blocks.OfType<EventBlock>())
                    {
                        runtime.Reset(evt.Id);
                    }

                    await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, token).ConfigureAwait(false);
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

    private static void ArmEvents(IReadOnlyList<MacroBlock> blocks, ScriptRuntime runtime)
    {
        foreach (var evt in blocks.OfType<EventBlock>())
        {
            runtime.RegisterEvent(evt.Id);
        }
    }

    private async Task ExecuteRangeAsync(
        IReadOnlyList<MacroBlock> blocks,
        int start,
        int end,
        ScriptRuntime runtime,
        CancellationToken token)
    {
        for (var i = start; i < end;)
        {
            token.ThrowIfCancellationRequested();
            var block = blocks[i];

            switch (block)
            {
                case EventBlock:
                    // Declarative — armed for the whole run, nothing to execute.
                    i++;
                    break;

                case ContinueUntilBlock continueUntil:
                    i = await ExecuteContinueUntilAsync(blocks, i, end, continueUntil, runtime, token)
                        .ConfigureAwait(false);
                    break;

                case EndContinueBlock:
                    // Orphaned end marker; skip.
                    i++;
                    break;

                default:
                    StatusChanged?.Invoke(this, $"Running: {block.DisplayName} — {block.Summary}");
                    await ExecuteActionAsync(block, token).ConfigureAwait(false);
                    i++;
                    break;
            }
        }
    }

    private async Task<int> ExecuteContinueUntilAsync(
        IReadOnlyList<MacroBlock> blocks,
        int continueIndex,
        int rangeEnd,
        ContinueUntilBlock continueUntil,
        ScriptRuntime runtime,
        CancellationToken token)
    {
        var endIndex = FindMatchingEndContinue(blocks, continueIndex, rangeEnd);
        if (endIndex < 0)
        {
            throw new InvalidOperationException(
                $"Continue Until at position {continueIndex + 1} is missing a matching End Continue.");
        }

        if (continueUntil.EventBlockId is not { } eventId)
        {
            throw new InvalidOperationException("Continue Until has no event selected.");
        }

        var bodyStart = continueIndex + 1;
        var bodyEnd = endIndex;

        // Wait for a fresh press of the watched event for this region.
        runtime.Reset(eventId);
        StatusChanged?.Invoke(this, $"Continue until {continueUntil.EventLabel}…");

        while (!runtime.IsTriggered(eventId))
        {
            token.ThrowIfCancellationRequested();

            if (bodyStart >= bodyEnd)
            {
                // No body — idle-wait for the event.
                await Task.Delay(25, token).ConfigureAwait(false);
                continue;
            }

            await ExecuteRangeAsync(blocks, bodyStart, bodyEnd, runtime, token).ConfigureAwait(false);

            if (!runtime.IsTriggered(eventId))
            {
                // Brief yield so a tight action loop still lets the key hook run.
                await Task.Delay(1, token).ConfigureAwait(false);
            }
        }

        StatusChanged?.Invoke(this, $"Event reached: {continueUntil.EventLabel}");
        return endIndex + 1;
    }

    private static int FindMatchingEndContinue(IReadOnlyList<MacroBlock> blocks, int continueIndex, int rangeEnd)
    {
        var depth = 0;
        for (var i = continueIndex + 1; i < rangeEnd; i++)
        {
            switch (blocks[i])
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

    private async Task ExecuteActionAsync(MacroBlock block, CancellationToken token)
    {
        switch (block)
        {
            case DelayBlock delay:
                await Task.Delay(Math.Max(0, delay.Milliseconds), token).ConfigureAwait(false);
                break;

            case MouseMoveBlock move:
                _input.MoveMouse(move.X, move.Y);
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
