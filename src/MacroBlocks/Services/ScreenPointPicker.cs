using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroBlocks.Native;

namespace MacroBlocks.Services;

/// <summary>
/// Captures the next left-click screen position via a low-level mouse hook.
/// Escape cancels. The captured click is swallowed so it does not activate UI underneath.
/// </summary>
public sealed class ScreenPointPicker : IDisposable
{
    private readonly NativeMethods.HookProc _mouseProc;
    private readonly NativeMethods.HookProc _keyboardProc;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private TaskCompletionSource<(int X, int Y)?>? _tcs;
    private CancellationTokenRegistration _ctr;
    private bool _disposed;

    public ScreenPointPicker()
    {
        // Keep delegates alive for the lifetime of the hook.
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public bool IsPicking => _tcs is { Task.IsCompleted: false };

    public Task<(int X, int Y)?> PickNextClickAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsPicking)
        {
            throw new InvalidOperationException("A point pick is already in progress.");
        }

        _tcs = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ctr = cancellationToken.Register(() => Complete(null));

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule
            ?? throw new InvalidOperationException("Unable to resolve the process module for hooking.");

        var moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc, moduleHandle, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc, moduleHandle, 0);

        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
        {
            TeardownHooks();
            var error = Marshal.GetLastWin32Error();
            _tcs = null;
            _ctr.Dispose();
            throw new InvalidOperationException($"Failed to install input hooks (Win32 error {error}).");
        }

        return _tcs.Task;
    }

    public void Cancel() => Complete(null);

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WmLButtonDown && _tcs is { Task.IsCompleted: false })
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            Complete((data.pt.X, data.pt.Y));
            // Swallow the click so it does not activate whatever is under the cursor.
            return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && (wParam == (IntPtr)NativeMethods.WmKeyDown || wParam == (IntPtr)NativeMethods.WmSysKeyDown)
            && _tcs is { Task.IsCompleted: false })
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == NativeMethods.VkEscape)
            {
                Complete(null);
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void Complete((int X, int Y)? point)
    {
        var tcs = _tcs;
        if (tcs is null || tcs.Task.IsCompleted)
        {
            return;
        }

        TeardownHooks();
        _ctr.Dispose();
        _tcs = null;
        tcs.TrySetResult(point);
    }

    private void TeardownHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Complete(null);
        _disposed = true;
    }
}
