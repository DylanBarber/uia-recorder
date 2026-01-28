using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UiaRecorder.Interop;

namespace UiaRecorder.Services;

public sealed class KeyCaptureEventArgs : EventArgs
{
    public DateTime TimestampUtc { get; init; }
    public string? Text { get; init; }
    public string? SpecialKey { get; init; }
}

public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private WinApi.HookProc? _proc;

    public event EventHandler<KeyCaptureEventArgs>? KeyCaptured;

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _proc = HookCallback;
        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? module = currentProcess.MainModule;
        IntPtr moduleHandle = WinApi.GetModuleHandle(module?.ModuleName);
        _hookId = WinApi.SetWindowsHookEx(WinApi.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        WinApi.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WinApi.WM_KEYDOWN || wParam == (IntPtr)WinApi.WM_SYSKEYDOWN))
        {
            WinApi.KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<WinApi.KBDLLHOOKSTRUCT>(lParam);
            string? specialKey = TryGetSpecialKey(hookStruct.vkCode);
            if (specialKey is not null)
            {
                KeyCaptured?.Invoke(this, new KeyCaptureEventArgs
                {
                    TimestampUtc = DateTime.UtcNow,
                    SpecialKey = specialKey
                });
            }
            else
            {
                string? text = TryTranslateKey(hookStruct.vkCode, hookStruct.scanCode, hookStruct.flags);
                if (!string.IsNullOrEmpty(text))
                {
                    KeyCaptured?.Invoke(this, new KeyCaptureEventArgs
                    {
                        TimestampUtc = DateTime.UtcNow,
                        Text = text
                    });
                }
            }
        }

        return WinApi.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string? TryGetSpecialKey(uint vkCode)
    {
        return vkCode switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            0x2E => "Delete",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            _ => null
        };
    }

    private static string? TryTranslateKey(uint vkCode, uint scanCode, uint flags)
    {
        byte[] keyState = new byte[256];
        if (WinApi.GetKeyboardState(keyState) == 0)
        {
            return null;
        }

        IntPtr keyboardLayout = WinApi.GetKeyboardLayout(0);
        StringBuilder buffer = new StringBuilder(8);
        int result = WinApi.ToUnicodeEx(vkCode, scanCode, keyState, buffer, buffer.Capacity, flags, keyboardLayout);
        if (result <= 0)
        {
            return null;
        }

        string text = buffer.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
