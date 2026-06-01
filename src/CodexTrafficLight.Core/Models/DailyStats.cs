namespace CodexTrafficLight.Core.Models;

public sealed record DailyStats
{
    public int RedCount { get; init; }
    public int GreenCount { get; init; }
    public long RedDurationMs { get; init; }
}
