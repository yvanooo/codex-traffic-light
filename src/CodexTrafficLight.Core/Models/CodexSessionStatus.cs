namespace CodexTrafficLight.Core.Models;

public sealed record CodexSessionStatus
{
    public string SessionId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public CodexLightState State { get; init; } = CodexLightState.Unknown;
    public string Event { get; init; } = "unknown";
    public int ProcessId { get; init; }
    public DateTimeOffset? ProcessStartTime { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
