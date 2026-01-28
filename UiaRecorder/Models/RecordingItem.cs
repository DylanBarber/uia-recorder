namespace UiaRecorder.Models;

public sealed record RecordingItem
{
    public string SessionId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
