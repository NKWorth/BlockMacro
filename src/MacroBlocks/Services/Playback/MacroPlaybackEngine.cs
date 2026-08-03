using MacroBlocks.Models;
using MacroBlocks.Models.Graph;
using MacroBlocks.Services.Vision;

namespace MacroBlocks.Services.Playback;

public sealed class MacroPlaybackEngine
{
    private readonly IInputSimulator _input;
    private readonly IScriptLibrary _library;
    private readonly IImageMatcher _imageMatcher;
    private CancellationTokenSource? _cts;
    private Task? _running;

    public MacroPlaybackEngine(IInputSimulator input, IScriptLibrary library)
        : this(input, library, new OpenCvImageMatcher())
    {
    }

    public MacroPlaybackEngine(IInputSimulator input, IScriptLibrary library, IImageMatcher imageMatcher)
    {
        _input = input;
        _library = library;
        _imageMatcher = imageMatcher;
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

                    if (script.FlowGraph.HasNodes)
                    {
                        await ExecuteGraphAsync(script.FlowGraph, runtime, callStack, token)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        runtime.BeginScriptOutput();
                        var blocks = script.Blocks.ToArray();
                        await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, callStack, token)
                            .ConfigureAwait(false);
                    }
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
                EnqueueLibrary(call.ScriptId, queue);
            }

            foreach (var node in current.FlowGraph.Nodes.Where(n => n.Kind == FlowGraphNodeKind.RunScript))
            {
                EnqueueLibrary(node.ScriptId, queue);
            }
        }
    }

    private void EnqueueLibrary(Guid? scriptId, Queue<MacroScript> queue)
    {
        if (scriptId is not { } id)
        {
            return;
        }

        var sub = _library.Get(id);
        if (sub is not null)
        {
            queue.Enqueue(sub);
        }
    }

    private async Task ExecuteGraphAsync(
        FlowGraph graph,
        ScriptRuntime runtime,
        Stack<Guid> callStack,
        CancellationToken token)
    {
        var entry = graph.FindEntryNode()
            ?? throw new InvalidOperationException("Flow graph has no entry node.");

        var current = entry;
        var visited = new HashSet<Guid>();

        while (current is not null)
        {
            token.ThrowIfCancellationRequested();

            if (!visited.Add(current.Id))
            {
                throw new InvalidOperationException(
                    $"Cycle detected in flow graph at node '{current.DisplayTitle}'.");
            }

            switch (current.Kind)
            {
                case FlowGraphNodeKind.RunScript:
                {
                    var output = await ExecuteGraphRunScriptAsync(current, runtime, callStack, token)
                        .ConfigureAwait(false);
                    runtime.RememberNodeOutput(current.Id, output);

                    var next = graph.FindOutbound(current.Id, FlowGraphPort.Next);
                    current = next is null
                        ? null
                        : graph.Nodes.FirstOrDefault(n => n.Id == next.ToId);
                    break;
                }

                case FlowGraphNodeKind.If:
                {
                    var conditionEdge = graph.FindInboundCondition(current.Id);
                    var condition = conditionEdge is not null
                        && runtime.GetNodeOutput(conditionEdge.FromId);

                    StatusChanged?.Invoke(
                        this,
                        condition ? "If: true → Then" : "If: false → Else");

                    var port = condition ? FlowGraphPort.Then : FlowGraphPort.Else;
                    var branch = graph.FindOutbound(current.Id, port);
                    current = branch is null
                        ? null
                        : graph.Nodes.FirstOrDefault(n => n.Id == branch.ToId);
                    break;
                }

                default:
                    throw new NotSupportedException($"Unknown graph node kind: {current.Kind}");
            }
        }
    }

    private async Task<bool> ExecuteGraphRunScriptAsync(
        FlowGraphNode node,
        ScriptRuntime runtime,
        Stack<Guid> callStack,
        CancellationToken token)
    {
        if (node.ScriptId is not { } scriptId)
        {
            throw new InvalidOperationException($"Graph node '{node.Label}' has no script selected.");
        }

        if (callStack.Contains(scriptId))
        {
            throw new InvalidOperationException(
                $"Circular subscript reference detected while calling '{node.Label}'.");
        }

        var sub = _library.Get(scriptId)
            ?? throw new InvalidOperationException($"Subscript '{node.Label}' was not found in the library.");

        StatusChanged?.Invoke(this, $"Graph: {node.Label}");
        callStack.Push(scriptId);
        try
        {
            runtime.BeginScriptOutput();
            var blocks = sub.Blocks.ToArray();
            await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, callStack, token)
                .ConfigureAwait(false);
            return runtime.CurrentBooleanOutput;
        }
        finally
        {
            callStack.Pop();
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

                case ReturnBooleanBlock ret:
                    runtime.SetBooleanOutput(ret.Value);
                    StatusChanged?.Invoke(this, $"Return Boolean: {ret.Summary}");
                    break;

                case FindImageBlock find:
                    StatusChanged?.Invoke(this, $"Running: {find.DisplayName} — {find.Summary}");
                    await ExecuteFindImageAsync(find, runtime, token).ConfigureAwait(false);
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
            var previous = runtime.CurrentBooleanOutput;
            runtime.BeginScriptOutput();
            var blocks = sub.Blocks.ToArray();
            await ExecuteRangeAsync(blocks, 0, blocks.Length, runtime, callStack, token)
                .ConfigureAwait(false);
            // Nested subscript output does not overwrite the caller's Return Boolean unless desired;
            // restore caller output so only explicit Return Boolean in this frame counts.
            runtime.SetBooleanOutput(previous);
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

    private async Task ExecuteFindImageAsync(
        FindImageBlock find,
        ScriptRuntime runtime,
        CancellationToken token)
    {
        TemplateImageStore.EnsureDirectory();
        var result = await Task.Run(() => _imageMatcher.Find(find, TemplateImageStore.DirectoryPath), token)
            .ConfigureAwait(false);
        runtime.SetBooleanOutput(result.Found);
        StatusChanged?.Invoke(
            this,
            result.Found
                ? $"Find Image: found (score {result.Score:P0})"
                : $"Find Image: not found (best {result.Score:P0}, need ≥ {find.Confidence:P0})");
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
