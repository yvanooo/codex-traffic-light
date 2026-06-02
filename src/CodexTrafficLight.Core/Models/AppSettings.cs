namespace CodexTrafficLight.Core.Models;

public sealed record AppSettings
{
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public string Theme { get; init; } = "dark";
    public string Style { get; init; } = "triple";
    public bool Muted { get; init; }
    public bool AutoOpenDrawerOnYellow { get; init; } = true;
    public bool ShowEndedSessions { get; init; }
    public bool Topmost { get; init; } = true;
    public string LampEffect { get; init; } = "breath";
    public string LampSpeed { get; init; } = "standard";
}
