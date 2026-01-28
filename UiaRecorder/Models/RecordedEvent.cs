using System.Collections.Generic;

namespace UiaRecorder.Models;

public sealed record RecordedEvent
{
    public DateTime TimestampUtc { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? ProcessName { get; init; }
    public int ProcessId { get; init; }
    public string? WindowTitle { get; init; }
    public ElementInfo? Element { get; init; }
    public string? Value { get; init; }
    public string? Text { get; init; }
    public string[]? Keys { get; init; }
    public Dictionary<string, string>? Context { get; init; }
}
