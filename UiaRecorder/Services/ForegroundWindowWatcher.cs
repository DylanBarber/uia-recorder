using System;
using System.Diagnostics;
using System.Text;
using UiaRecorder.Interop;

namespace UiaRecorder.Services;

public sealed class ForegroundWindowChangedEventArgs : EventArgs
{
    public string? WindowTitle { get; init; }
    public int ProcessId { get; init; }
    public string? ProcessName { get; init; }
}

public sealed class ForegroundWindowWatcher : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private WinApi.WinEventDelegate? _callback;

    public event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundChanged;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _callback = HandleWinEvent;
        _hookHandle = WinApi.SetWinEventHook(
            WinApi.EVENT_SYSTEM_FOREGROUND,
            WinApi.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WinApi.WINEVENT_OUTOFCONTEXT);

        RaiseForegroundChanged();
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        WinApi.UnhookWinEvent(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _callback = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void HandleWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        RaiseForegroundChanged(hwnd);
    }

    private void RaiseForegroundChanged(IntPtr? hwndOverride = null)
    {
        IntPtr hwnd = hwndOverride ?? WinApi.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        string? title = GetWindowTitle(hwnd);
        int processId = GetProcessId(hwnd);
        string? processName = null;
        if (processId > 0)
        {
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
            }
            catch
            {
            }
        }

        ForegroundChanged?.Invoke(this, new ForegroundWindowChangedEventArgs
        {
            WindowTitle = title,
            ProcessId = processId,
            ProcessName = processName
        });
    }

    private static string? GetWindowTitle(IntPtr hwnd)
    {
        int length = WinApi.GetWindowTextLength(hwnd);
        if (length == 0)
        {
            return null;
        }

        StringBuilder builder = new StringBuilder(length + 1);
        _ = WinApi.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static int GetProcessId(IntPtr hwnd)
    {
        _ = WinApi.GetWindowThreadProcessId(hwnd, out uint pid);
        return unchecked((int)pid);
    }
}
