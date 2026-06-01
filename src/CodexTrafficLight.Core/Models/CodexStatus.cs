namespace CodexTrafficLight.Core.Models;

public sealed record CodexStatus(
    CodexLightState State,
    string Event,
    DateTimeOffset UpdatedAt);
