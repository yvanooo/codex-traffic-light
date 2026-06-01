using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

public sealed class SessionStatusStoreTests
{
    [Fact]
    public void LoadActiveSessionsSortsByPriorityAndComputesAggregateState()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("mes", CodexLightState.Red, @"F:\Mes", now.AddSeconds(-102)));
        store.Write(CreateSession("codex", CodexLightState.Yellow, @"C:\Users\28022\Desktop\codex", now.AddSeconds(-12)));
        store.Write(CreateSession("docs", CodexLightState.Green, @"C:\Users\28022\Desktop\上下文", now.AddMinutes(-1)));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(3, sessions.Count);
        Assert.Equal("codex", sessions[0].SessionId);
        Assert.Equal("mes", sessions[1].SessionId);
        Assert.Equal("docs", sessions[2].SessionId);
        Assert.Equal(CodexLightState.Yellow, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldGreenSessionsWhenProcessIsGoneAndSkipsInvalidFiles()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("fresh-green", CodexLightState.Green, @"F:\Fresh", now.AddMinutes(-4)));
        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Old", now.AddMinutes(-6)));
        Directory.CreateDirectory(paths.SessionDirectory);
        File.WriteAllText(Path.Combine(paths.SessionDirectory, "broken.json"), "{bad-json");

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("fresh-green", sessions[0].SessionId);
        Assert.Equal(CodexLightState.Green, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadSessionsIncludesEndedGreenSessionsWhenRequested()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("fresh-green", CodexLightState.Green, @"F:\Fresh", now.AddMinutes(-4)));
        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Old", now.AddMinutes(-45)));
        store.Write(CreateSession("stale-red", CodexLightState.Red, @"F:\Stale", now.AddMinutes(-45)));

        var visible = store.LoadVisibleSessions(now);
        var withEnded = store.LoadSessions(includeEnded: true, now);

        Assert.Single(visible);
        Assert.Equal(2, withEnded.Count);
        Assert.Contains(withEnded, session => session.SessionId == "fresh-green");
        Assert.Contains(withEnded, session => session.SessionId == "old-green");
        Assert.DoesNotContain(withEnded, session => session.SessionId == "stale-red");
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldGreenSessionsEvenWhenProcessIsStillRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, processId => processId == 123);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-6), source: "cli"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Empty(sessions);
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldVsCodePluginGreenSessionsEvenWhenAppServerIsRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => true);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("codex-session-id", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-31), source: "vscode-plugin"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Empty(sessions);
    }

    [Fact]
    public void LoadVisibleSessionsHidesYellowAfterFiveMinutesAndRedAfterTenMinutes()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("stale-yellow", CodexLightState.Yellow, @"F:\YellowOld", now.AddMinutes(-6)));
        store.Write(CreateSession("fresh-yellow", CodexLightState.Yellow, @"F:\YellowFresh", now.AddMinutes(-4)));
        store.Write(CreateSession("stale-red", CodexLightState.Red, @"F:\RedOld", now.AddMinutes(-11)));
        store.Write(CreateSession("fresh-red", CodexLightState.Red, @"F:\RedFresh", now.AddMinutes(-9)));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "fresh-yellow");
        Assert.Contains(sessions, session => session.SessionId == "fresh-red");
        Assert.DoesNotContain(sessions, session => session.SessionId == "stale-yellow");
        Assert.DoesNotContain(sessions, session => session.SessionId == "stale-red");
    }

    [Fact]
    public void WriteOverwritesSameSessionIdInsteadOfCreatingDuplicateRows()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("same-session", CodexLightState.Red, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("same-session", CodexLightState.Green, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal(CodexLightState.Green, sessions[0].State);
        Assert.Single(Directory.GetFiles(paths.SessionDirectory, "*.json"));
    }

    [Fact]
    public void LoadVisibleSessionsKeepsReliableSessionsWithSameWorkingDirectory()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("codex-session-a", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("codex-session-b", CodexLightState.Red, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "codex-session-a");
        Assert.Contains(sessions, session => session.SessionId == "codex-session-b");
    }

    [Fact]
    public void LoadVisibleSessionsDeduplicatesOnlyWeakPidSessionsByWorkingDirectory()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("pid-111-unknownstart", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("pid-222-unknownstart", CodexLightState.Red, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("pid-222-unknownstart", sessions[0].SessionId);
    }

    [Fact]
    public void GetAggregateStateReturnsUnknownWhenNoSessionsAreVisible()
    {
        Assert.Equal(CodexLightState.Unknown, SessionStatusStore.GetAggregateState(Array.Empty<CodexSessionStatus>()));
    }

    private static CodexSessionStatus CreateSession(
        string id,
        CodexLightState state,
        string workingDirectory,
        DateTimeOffset updatedAt,
        string source = "cli")
    {
        return new CodexSessionStatus
        {
            SessionId = id,
            DisplayName = Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            WorkingDirectory = workingDirectory,
            State = state,
            Event = state switch
            {
                CodexLightState.Red => "UserPromptSubmit",
                CodexLightState.Yellow => "PermissionRequest",
                CodexLightState.Green => "Stop",
                _ => "unknown"
            },
            ProcessId = 123,
            ProcessStartTime = DateTimeOffset.Parse("2026-06-01T15:00:00+08:00"),
            UpdatedAt = updatedAt,
            Source = source
        };
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
