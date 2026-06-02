using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

public sealed class StatsStoreTests
{
    [Fact]
    public void RecordsRedAndGreenCounts()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);

        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, day);
        store.RecordStateChange(
            CodexLightState.Green,
            CodexLightState.Red,
            DateTimeOffset.Parse("2026-06-01T10:00:00+08:00"),
            day,
            DateTimeOffset.Parse("2026-06-01T10:05:00+08:00"));

        var stats = store.Load();

        Assert.Equal(1, stats["2026-06-01"].RedCount);
        Assert.Equal(1, stats["2026-06-01"].GreenCount);
        Assert.Equal(300000, stats["2026-06-01"].RedDurationMs);
    }

    [Fact]
    public void DoesNotRecordManualOrUnknownStates()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);

        store.RecordStateChange(CodexLightState.Yellow, CodexLightState.Unknown, null, day);
        store.RecordStateChange(CodexLightState.Unknown, CodexLightState.Yellow, null, day);

        var stats = store.Load();

        Assert.Equal(0, stats["2026-06-01"].RedCount);
        Assert.Equal(0, stats["2026-06-01"].GreenCount);
        Assert.Equal(0, stats["2026-06-01"].RedDurationMs);
    }

    [Fact]
    public void RecordsAggregateSessionStateChangesForWeeklyReport()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);
        var redStartedAt = DateTimeOffset.Parse("2026-06-01T10:00:00+08:00");

        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, day, redStartedAt);
        store.RecordStateChange(
            CodexLightState.Green,
            CodexLightState.Red,
            redStartedAt,
            day,
            DateTimeOffset.Parse("2026-06-01T10:03:00+08:00"));

        var stats = store.Load();

        Assert.Equal(1, stats["2026-06-01"].RedCount);
        Assert.Equal(1, stats["2026-06-01"].GreenCount);
        Assert.Equal(180000, stats["2026-06-01"].RedDurationMs);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
