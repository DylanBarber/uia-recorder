using System;
using System.Collections.Concurrent;
using System.Windows.Automation;

namespace UiaRecorder.Services;

public sealed class UiaEventArgs : EventArgs
{
    public AutomationElement Element { get; init; } = AutomationElement.RootElement;
    public string EventType { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed class UiaEventListener : IDisposable
{
    private readonly int _ignoreProcessId;
    private bool _started;
    private readonly ConcurrentDictionary<string, DateTime> _lastEventByKey = new();

    public event EventHandler<UiaEventArgs>? AutomationEvent;
    public event EventHandler<UiaEventArgs>? FocusChanged;

    public UiaEventListener(int ignoreProcessId)
    {
        _ignoreProcessId = ignoreProcessId;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
        Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Subtree, OnAutomationEvent);
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, TreeScope.Subtree, OnAutomationEvent);
        Automation.AddAutomationEventHandler(InvokePattern.InvokedEvent, AutomationElement.RootElement, TreeScope.Subtree, OnAutomationEvent);
        Automation.AddAutomationEventHandler(TextPattern.TextChangedEvent, AutomationElement.RootElement, TreeScope.Subtree, OnAutomationEvent);
        Automation.AddAutomationEventHandler(SelectionItemPattern.ElementSelectedEvent, AutomationElement.RootElement, TreeScope.Subtree, OnAutomationEvent);
        Automation.AddAutomationPropertyChangedEventHandler(
            AutomationElement.RootElement,
            TreeScope.Subtree,
            OnPropertyChanged,
            ValuePattern.ValueProperty);

        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        Automation.RemoveAutomationFocusChangedEventHandler(OnFocusChanged);
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, OnAutomationEvent);
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, OnAutomationEvent);
        Automation.RemoveAutomationEventHandler(InvokePattern.InvokedEvent, AutomationElement.RootElement, OnAutomationEvent);
        Automation.RemoveAutomationEventHandler(TextPattern.TextChangedEvent, AutomationElement.RootElement, OnAutomationEvent);
        Automation.RemoveAutomationEventHandler(SelectionItemPattern.ElementSelectedEvent, AutomationElement.RootElement, OnAutomationEvent);
        Automation.RemoveAutomationPropertyChangedEventHandler(AutomationElement.RootElement, OnPropertyChanged);

        _started = false;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        AutomationElement element = AutomationElement.FocusedElement;
        if (ShouldIgnoreElement(element))
        {
            return;
        }

        FocusChanged?.Invoke(this, new UiaEventArgs
        {
            Element = element,
            EventType = "FocusChanged"
        });
    }

    private void OnAutomationEvent(object sender, AutomationEventArgs e)
    {
        if (sender is not AutomationElement element || ShouldIgnoreElement(element))
        {
            return;
        }

        string eventType = e.EventId.ProgrammaticName switch
        {
            var id when id == WindowPattern.WindowOpenedEvent.ProgrammaticName => "WindowOpened",
            var id when id == WindowPattern.WindowClosedEvent.ProgrammaticName => "WindowClosed",
            var id when id == InvokePattern.InvokedEvent.ProgrammaticName => "Invoke",
            var id when id == TextPattern.TextChangedEvent.ProgrammaticName => "TextChanged",
            var id when id == SelectionItemPattern.ElementSelectedEvent.ProgrammaticName => "SelectionChanged",
            _ => e.EventId.ProgrammaticName
        };

        if (IsDebounced(element, eventType, TimeSpan.FromMilliseconds(200)))
        {
            return;
        }

        AutomationEvent?.Invoke(this, new UiaEventArgs
        {
            Element = element,
            EventType = eventType,
            Value = TryGetElementValue(element, eventType)
        });
    }

    private void OnPropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
    {
        if (sender is not AutomationElement element || ShouldIgnoreElement(element))
        {
            return;
        }

        if (IsDebounced(element, "ValueChanged", TimeSpan.FromMilliseconds(200)))
        {
            return;
        }

        AutomationEvent?.Invoke(this, new UiaEventArgs
        {
            Element = element,
            EventType = "ValueChanged",
            Value = e.NewValue?.ToString()
        });
    }

    private bool ShouldIgnoreElement(AutomationElement element)
    {
        try
        {
            return element.Current.ProcessId == _ignoreProcessId;
        }
        catch
        {
            return true;
        }
    }

    private bool IsDebounced(AutomationElement element, string eventType, TimeSpan debounceWindow)
    {
        string key = GetElementKey(element, eventType);
        DateTime now = DateTime.UtcNow;
        if (_lastEventByKey.TryGetValue(key, out DateTime last) && now - last < debounceWindow)
        {
            return true;
        }

        _lastEventByKey[key] = now;
        return false;
    }

    private static string GetElementKey(AutomationElement element, string eventType)
    {
        try
        {
            int[] runtimeId = element.GetRuntimeId();
            return string.Join("-", runtimeId) + "-" + eventType;
        }
        catch
        {
            string id = string.Empty;
            try
            {
                id = element.Current.AutomationId;
            }
            catch
            {
            }

            return id + "-" + eventType;
        }
    }

    private static string? TryGetElementValue(AutomationElement element, string eventType)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObj) && valuePatternObj is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value;
            }

            if (eventType == "TextChanged" && element.TryGetCurrentPattern(TextPattern.Pattern, out object? textPatternObj) && textPatternObj is TextPattern textPattern)
            {
                return textPattern.DocumentRange.GetText(-1);
            }
        }
        catch
        {
        }

        return null;
    }
}
