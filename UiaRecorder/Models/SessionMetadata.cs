namespace UiaRecorder.Models;

public sealed record SessionMetadata
{
    public string SessionId { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string OutputDirectory { get; init; } = string.Empty;
    public bool AllowPasswordCapture { get; init; }
    public string AppVersion { get; init; } = string.Empty;
}
