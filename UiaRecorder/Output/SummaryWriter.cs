using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UiaRecorder.Models;

namespace UiaRecorder.Output;

public static class SummaryWriter
{
    public static void Write(string filePath, SessionMetadata session, IReadOnlyCollection<RecordedEvent> events)
    {
        StringBuilder builder = new();
        TimeSpan duration = session.EndUtc - session.StartUtc;
        builder.AppendLine($"Session {session.SessionId} ({duration:hh\\:mm\\:ss})");
        builder.AppendLine($"Output: {session.OutputDirectory}");
        builder.AppendLine($"Capture passwords: {(session.AllowPasswordCapture ? "Yes" : "No")}");
        builder.AppendLine();

        foreach (RecordedEvent recordedEvent in events.OrderBy(e => e.TimestampUtc))
        {
            string time = recordedEvent.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
            builder.AppendLine($"{time} {FormatEvent(recordedEvent)}");
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private static string FormatEvent(RecordedEvent recordedEvent)
    {
        bool redacted = recordedEvent.Context != null && recordedEvent.Context.TryGetValue("redacted", out string? value) && value == "true";
        string window = string.IsNullOrWhiteSpace(recordedEvent.WindowTitle) ? "Unknown Window" : recordedEvent.WindowTitle;
        string? elementLabel = FormatElement(recordedEvent.Element);
        string elementDisplay = string.IsNullOrWhiteSpace(elementLabel) ? "element" : elementLabel;

        return recordedEvent.EventType switch
        {
            "FocusChanged" => $"Focused {elementDisplay} in \"{window}\"",
            "WindowOpened" => $"Window opened \"{window}\"",
            "WindowClosed" => $"Window closed \"{window}\"",
            "ForegroundChanged" => $"Foreground changed to \"{window}\"",
            "Invoke" => $"Invoked {elementDisplay} in \"{window}\"",
            "SelectionChanged" => $"Selection changed in {elementDisplay} to \"{recordedEvent.Value}\"",
            "ValueChanged" => $"Changed {elementDisplay} value to \"{FormatValue(recordedEvent.Value, redacted)}\"",
            "TextChanged" => $"Text changed in {elementDisplay}",
            "KeyText" => $"Typed \"{FormatValue(recordedEvent.Text, redacted)}\" in {elementDisplay}",
            "KeySpecial" => $"Pressed {FormatKeys(recordedEvent.Keys, redacted)} in \"{window}\"",
            _ => $"{recordedEvent.EventType} in \"{window}\""
        };
    }

    private static string? FormatElement(ElementInfo? element)
    {
        if (element is null)
        {
            return null;
        }

        string controlType = element.ControlType ?? "Control";
        if (!string.IsNullOrWhiteSpace(element.Name))
        {
            return $"{controlType} '{element.Name}'";
        }

        return controlType;
    }

    private static string FormatValue(string? value, bool redacted)
    {
        if (redacted)
        {
            return "[REDACTED]";
        }

        return value ?? string.Empty;
    }

    private static string FormatKeys(string[]? keys, bool redacted)
    {
        if (redacted)
        {
            return "[REDACTED]";
        }

        if (keys == null || keys.Length == 0)
        {
            return "key";
        }

        return string.Join(", ", keys);
    }
}
