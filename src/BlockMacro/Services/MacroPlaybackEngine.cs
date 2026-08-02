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
            try
            {
                Started?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, "Running");

                do
                {
                    foreach (var block in script.Blocks.ToArray())
                    {
                        token.ThrowIfCancellationRequested();
                        StatusChanged?.Invoke(this, $"Running: {block.DisplayName} — {block.Summary}");
                        await ExecuteBlockAsync(block, token).ConfigureAwait(false);
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

    private async Task ExecuteBlockAsync(MacroBlock block, CancellationToken token)
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
