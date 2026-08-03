using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroBlocks.Models;
using MacroBlocks.Native;

namespace MacroBlocks.Services;

/// <summary>
/// While a script runs, watches for manual key presses that match configured Press Key events.
/// Injected keys (from SendInput) are ignored so scripted Key Press blocks do not self-trigger.
/// </summary>
public sealed class KeyPressEventWatcher : IDisposable
{
    private const uint LlKhfInjected = 0x10;

    private readonly NativeMethods.HookProc _proc;
    private readonly Dictionary<ushort, List<Guid>> _eventsByKey = [];
    private readonly ScriptRuntime _runtime;
    private IntPtr _hook;
    private bool _disposed;

    public KeyPressEventWatcher(ScriptRuntime runtime, IEnumerable<KeyPressEventBlock> events)
    {
        _runtime = runtime;
        _proc = HookCallback;

        foreach (var evt in events)
        {
            if (!_eventsByKey.TryGetValue(evt.VirtualKey, out var list))
            {
                list = [];
                _eventsByKey[evt.VirtualKey] = list;
            }

            list.Add(evt.Id);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_eventsByKey.Count == 0 || _hook != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule
            ?? throw new InvalidOperationException("Unable to resolve the process module for hooking.");

        var moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _proc, moduleHandle, 0);
        if (_hook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install key event hook (Win32 error {error}).");
        }
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && (wParam == (IntPtr)NativeMethods.WmKeyDown || wParam == (IntPtr)NativeMethods.WmSysKeyDown))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var injected = (data.flags & LlKhfInjected) != 0;
            if (!injected && _eventsByKey.TryGetValue((ushort)data.vkCode, out var eventIds))
            {
                foreach (var id in eventIds)
                {
                    _runtime.Trigger(id);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
