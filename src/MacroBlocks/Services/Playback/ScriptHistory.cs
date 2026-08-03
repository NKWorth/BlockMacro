using System.Windows.Threading;
using MacroBlocks.Models;

namespace MacroBlocks.Services.Playback;

/// <summary>
/// Snapshot-based undo/redo for the working script tree.
/// </summary>
public sealed class ScriptHistory
{
    private const int MaxEntries = 100;

    private readonly Stack<MacroScript> _undo = new();
    private readonly Stack<MacroScript> _redo = new();
    private readonly DispatcherTimer _propertyDebounce;
    private MacroScript _baseline = new();
    private bool _propertySession;
    private bool _isApplying;
    private Func<MacroScript>? _getCurrent;

    public ScriptHistory()
    {
        _propertyDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _propertyDebounce.Tick += (_, _) =>
        {
            _propertyDebounce.Stop();
            if (_getCurrent is null)
            {
                return;
            }

            CaptureBaseline(_getCurrent());
        };
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? Changed;

    public void Attach(Func<MacroScript> getCurrent) => _getCurrent = getCurrent;

    public void Reset(MacroScript current)
    {
        _propertyDebounce.Stop();
        _undo.Clear();
        _redo.Clear();
        _propertySession = false;
        _baseline = current.CloneDeep();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Call immediately before a structural mutation.
    /// </summary>
    public void CheckpointBeforeChange(MacroScript current)
    {
        if (_isApplying)
        {
            return;
        }

        EndPropertySession(current);
        PushUndo(current.CloneDeep());
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Call after a structural mutation completes.
    /// </summary>
    public void CaptureBaseline(MacroScript current)
    {
        _propertyDebounce.Stop();
        _baseline = current.CloneDeep();
        _propertySession = false;
    }

    /// <summary>
    /// Call when a block property changes via the inspector (coalesced into one undo step).
    /// </summary>
    public void OnPropertyEdited(MacroScript current)
    {
        if (_isApplying)
        {
            return;
        }

        if (!_propertySession)
        {
            PushUndo(_baseline.CloneDeep());
            _redo.Clear();
            _propertySession = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        _propertyDebounce.Stop();
        _propertyDebounce.Start();
    }

    public MacroScript? Undo(MacroScript current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        _propertyDebounce.Stop();
        _propertySession = false;
        _redo.Push(current.CloneDeep());
        var previous = _undo.Pop();
        _baseline = previous.CloneDeep();
        Changed?.Invoke(this, EventArgs.Empty);
        return previous.CloneDeep();
    }

    public MacroScript? Redo(MacroScript current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        _propertyDebounce.Stop();
        _propertySession = false;
        _undo.Push(current.CloneDeep());
        var next = _redo.Pop();
        _baseline = next.CloneDeep();
        Changed?.Invoke(this, EventArgs.Empty);
        return next.CloneDeep();
    }

    private void EndPropertySession(MacroScript current)
    {
        _propertyDebounce.Stop();
        if (_propertySession)
        {
            _baseline = current.CloneDeep();
            _propertySession = false;
        }
    }

    private void PushUndo(MacroScript snapshot)
    {
        _undo.Push(snapshot);
        if (_undo.Count <= MaxEntries)
        {
            return;
        }

        var keep = _undo.Take(MaxEntries).Reverse().ToArray();
        _undo.Clear();
        foreach (var item in keep)
        {
            _undo.Push(item);
        }
    }

    public IDisposable ApplyScope()
    {
        _isApplying = true;
        return new ApplyToken(this);
    }

    private sealed class ApplyToken(ScriptHistory owner) : IDisposable
    {
        public void Dispose() => owner._isApplying = false;
    }
}
