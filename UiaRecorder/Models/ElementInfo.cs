namespace UiaRecorder.Models;

public sealed record ElementInfo
{
    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? ControlType { get; init; }
    public string? ClassName { get; init; }
    public bool IsPassword { get; init; }
}
