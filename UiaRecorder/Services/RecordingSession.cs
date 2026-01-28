using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Automation;
using UiaRecorder.Interop;
using UiaRecorder.Models;
using UiaRecorder.Output;

namespace UiaRecorder.Services;

public sealed class RecordingSession : IDisposable
{
    private readonly int _ignoreProcessId;
    private readonly ConcurrentQueue<RecordedEvent> _events = new();
    private readonly object _contextLock = new();
    private readonly StringBuilder _textBuffer = new();
    private readonly TimeSpan _textBurstGap = TimeSpan.FromMilliseconds(700);
    private readonly Dictionary<string, string> _redactedContext = new() { ["redacted"] = "true" };

    private UiaEventListener? _uiaListener;
    private KeyboardHook? _keyboardHook;
    private ForegroundWindowWatcher? _windowWatcher;
    private DateTime _startUtc;
    private DateTime _lastTextAt;
    private FocusContext _currentContext = new();
    private FocusContext _textContextSnapshot = new();
    private bool _isRecording;

    public event EventHandler<RecordedEvent>? EventRecorded;
    public event EventHandler? DroppedEvent;

    public bool AllowPasswordCapture { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public string SessionId { get; private set; } = string.Empty;

    public RecordingSession()
    {
        _ignoreProcessId = Process.GetCurrentProcess().Id;
    }

    public void Start()
    {
        if (_isRecording)
        {
            return;
        }

        _startUtc = DateTime.UtcNow;
        SessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        while (_events.TryDequeue(out _))
        {
        }
        _textBuffer.Clear();
        _lastTextAt = DateTime.MinValue;
        _currentContext = new FocusContext();
        _textContextSnapshot = new FocusContext();

        _uiaListener = new UiaEventListener(_ignoreProcessId);
        _uiaListener.FocusChanged += HandleFocusChanged;
        _uiaListener.AutomationEvent += HandleAutomationEvent;
        _uiaListener.Start();

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyCaptured += HandleKeyCaptured;
        _keyboardHook.Start();

        _windowWatcher = new ForegroundWindowWatcher();
        _windowWatcher.ForegroundChanged += HandleForegroundChanged;
        _windowWatcher.Start();

        _isRecording = true;
    }

    public void Stop()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        FlushTextBurst();

        if (_uiaListener != null)
        {
            _uiaListener.FocusChanged -= HandleFocusChanged;
            _uiaListener.AutomationEvent -= HandleAutomationEvent;
            _uiaListener.Dispose();
            _uiaListener = null;
        }

        if (_keyboardHook != null)
        {
            _keyboardHook.KeyCaptured -= HandleKeyCaptured;
            _keyboardHook.Dispose();
            _keyboardHook = null;
        }

        if (_windowWatcher != null)
        {
            _windowWatcher.ForegroundChanged -= HandleForegroundChanged;
            _windowWatcher.Dispose();
            _windowWatcher = null;
        }

        PersistOutput();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void HandleFocusChanged(object? sender, UiaEventArgs e)
    {
        UpdateContext(e.Element);
        RecordEvent(CreateEventFromElement(e.Element, "FocusChanged", null));
    }

    private void HandleAutomationEvent(object? sender, UiaEventArgs e)
    {
        RecordEvent(CreateEventFromElement(e.Element, e.EventType, e.Value));
    }

    private void HandleForegroundChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        if (e.ProcessId == _ignoreProcessId)
        {
            return;
        }

        lock (_contextLock)
        {
            _currentContext.WindowTitle = e.WindowTitle;
            _currentContext.ProcessId = e.ProcessId;
            _currentContext.ProcessName = e.ProcessName;
            _currentContext.Element = null;
        }

        RecordEvent(new RecordedEvent
        {
            TimestampUtc = DateTime.UtcNow,
            EventType = "ForegroundChanged",
            WindowTitle = e.WindowTitle,
            ProcessId = e.ProcessId,
            ProcessName = e.ProcessName
        });
    }

    private void HandleKeyCaptured(object? sender, KeyCaptureEventArgs e)
    {
        FocusContext context = GetCurrentContext();
        if (context.ProcessId == _ignoreProcessId)
        {
            return;
        }

        if (!string.IsNullOrEmpty(e.Text))
        {
            if (_textBuffer.Length == 0 || e.TimestampUtc - _lastTextAt > _textBurstGap)
            {
                FlushTextBurst();
                _textContextSnapshot = context;
            }

            _textBuffer.Append(e.Text);
            _lastTextAt = e.TimestampUtc;
        }

        if (!string.IsNullOrEmpty(e.SpecialKey))
        {
            FlushTextBurst();
            RecordEvent(CreateKeySpecialEvent(context, e.SpecialKey, e.TimestampUtc));
        }
    }

    private void FlushTextBurst()
    {
        if (_textBuffer.Length == 0)
        {
            return;
        }

        string text = _textBuffer.ToString();
        _textBuffer.Clear();
        DateTime timestamp = _lastTextAt == DateTime.MinValue ? DateTime.UtcNow : _lastTextAt;
        RecordEvent(CreateKeyTextEvent(_textContextSnapshot, text, timestamp));
    }

    private void RecordEvent(RecordedEvent? recordedEvent)
    {
        if (recordedEvent == null)
        {
            return;
        }

        _events.Enqueue(recordedEvent);
        EventRecorded?.Invoke(this, recordedEvent);
    }

    private RecordedEvent CreateKeyTextEvent(FocusContext context, string text, DateTime timestampUtc)
    {
        if (IsRedacted(context))
        {
            return new RecordedEvent
            {
                TimestampUtc = timestampUtc,
                EventType = "KeyText",
                ProcessId = context.ProcessId,
                ProcessName = context.ProcessName,
                WindowTitle = context.WindowTitle,
                Element = context.Element,
                Context = new Dictionary<string, string>(_redactedContext)
            };
        }

        return new RecordedEvent
        {
            TimestampUtc = timestampUtc,
            EventType = "KeyText",
            ProcessId = context.ProcessId,
            ProcessName = context.ProcessName,
            WindowTitle = context.WindowTitle,
            Element = context.Element,
            Text = text
        };
    }

    private RecordedEvent CreateKeySpecialEvent(FocusContext context, string key, DateTime timestampUtc)
    {
        if (IsRedacted(context))
        {
            return new RecordedEvent
            {
                TimestampUtc = timestampUtc,
                EventType = "KeySpecial",
                ProcessId = context.ProcessId,
                ProcessName = context.ProcessName,
                WindowTitle = context.WindowTitle,
                Element = context.Element,
                Context = new Dictionary<string, string>(_redactedContext)
            };
        }

        return new RecordedEvent
        {
            TimestampUtc = timestampUtc,
            EventType = "KeySpecial",
            ProcessId = context.ProcessId,
            ProcessName = context.ProcessName,
            WindowTitle = context.WindowTitle,
            Element = context.Element,
            Keys = new[] { key }
        };
    }

    private RecordedEvent? CreateEventFromElement(AutomationElement element, string eventType, string? value)
    {
        try
        {
            int processId = element.Current.ProcessId;
            string? processName = null;
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
            }
            catch
            {
            }

            string? windowTitle = TryGetWindowTitleFromElement(element);
            ElementInfo elementInfo = CreateElementInfo(element);

            bool isRedacted = elementInfo.IsPassword && !AllowPasswordCapture;
            return new RecordedEvent
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = eventType,
                ProcessId = processId,
                ProcessName = processName,
                WindowTitle = windowTitle,
                Element = elementInfo,
                Value = isRedacted ? null : value,
                Context = isRedacted ? new Dictionary<string, string>(_redactedContext) : null
            };
        }
        catch
        {
            DroppedEvent?.Invoke(this, EventArgs.Empty);
            return null;
        }
    }

    private void UpdateContext(AutomationElement element)
    {
        try
        {
            ElementInfo info = CreateElementInfo(element);
            string? windowTitle = TryGetWindowTitleFromElement(element);
            int processId = element.Current.ProcessId;
            string? processName = null;
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
            }
            catch
            {
            }

            lock (_contextLock)
            {
                _currentContext.Element = info;
                _currentContext.WindowTitle = windowTitle;
                _currentContext.ProcessId = processId;
                _currentContext.ProcessName = processName;
            }
        }
        catch
        {
        }
    }

    private FocusContext GetCurrentContext()
    {
        lock (_contextLock)
        {
            return _currentContext.Clone();
        }
    }

    private static ElementInfo CreateElementInfo(AutomationElement element)
    {
        return new ElementInfo
        {
            Name = SafeGet(() => element.Current.Name),
            AutomationId = SafeGet(() => element.Current.AutomationId),
            ControlType = SafeGet(() => element.Current.ControlType.ProgrammaticName),
            ClassName = SafeGet(() => element.Current.ClassName),
            IsPassword = SafeGet(() => element.Current.IsPassword)
        };
    }

    private static string? TryGetWindowTitleFromElement(AutomationElement element)
    {
        try
        {
            AutomationElement? windowElement = element;
            try
            {
                while (windowElement != null && windowElement.Current.ControlType != ControlType.Window)
                {
                    windowElement = TreeWalker.ControlViewWalker.GetParent(windowElement);
                }
            }
            catch
            {
                windowElement = element;
            }

            int hwnd = windowElement?.Current.NativeWindowHandle ?? 0;
            if (hwnd == 0)
            {
                return null;
            }

            int length = WinApi.GetWindowTextLength((IntPtr)hwnd);
            if (length == 0)
            {
                return null;
            }

            StringBuilder builder = new(length + 1);
            _ = WinApi.GetWindowText((IntPtr)hwnd, builder, builder.Capacity);
            return builder.ToString();
        }
        catch
        {
            return null;
        }
    }

    private bool IsRedacted(FocusContext context)
    {
        return context.Element?.IsPassword == true && !AllowPasswordCapture;
    }

    private void PersistOutput()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(OutputDirectory);
        SessionMetadata session = new()
        {
            SessionId = SessionId,
            StartUtc = _startUtc,
            EndUtc = DateTime.UtcNow,
            OutputDirectory = OutputDirectory,
            AllowPasswordCapture = AllowPasswordCapture,
            AppVersion = GetAppVersion()
        };

        List<RecordedEvent> eventList = new(_events);
        string jsonPath = Path.Combine(OutputDirectory, $"session-{SessionId}.json");
        string summaryPath = Path.Combine(OutputDirectory, $"session-{SessionId}-summary.txt");
        JsonWriter.Write(jsonPath, session, eventList);
        SummaryWriter.Write(summaryPath, session, eventList);
    }

    private static string GetAppVersion()
    {
        return typeof(RecordingSession).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static T? SafeGet<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return default;
        }
    }

    private sealed class FocusContext
    {
        public ElementInfo? Element { get; set; }
        public string? WindowTitle { get; set; }
        public int ProcessId { get; set; }
        public string? ProcessName { get; set; }

        public FocusContext Clone()
        {
            return new FocusContext
            {
                Element = Element,
                WindowTitle = WindowTitle,
                ProcessId = ProcessId,
                ProcessName = ProcessName
            };
        }
    }
}
