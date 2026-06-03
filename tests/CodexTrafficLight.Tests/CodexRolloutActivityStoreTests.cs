using System.Text.Json;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

public sealed class CodexRolloutActivityStoreTests
{
    [Fact]
    public void LoadActiveSessionsReturnsRedForStartedTurnWithoutCompletion()
    {
        var paths = new CodexPaths(CreateTempRoot());
        WriteRollout(paths, "thread-1", """
        {"timestamp":"2026-06-03T09:32:00Z","type":"session_meta","payload":{"id":"thread-1","cwd":"D:\\workspace\\datespace","source":"vscode"}}
        {"timestamp":"2026-06-03T09:32:01Z","type":"event_msg","payload":{"type":"user_message","message":"项目任务测试\n"}}
        {"timestamp":"2026-06-03T09:32:02Z","type":"event_msg","payload":{"type":"task_started","turn_id":"turn-1","started_at":1780479122}}
        """);

        var store = new CodexRolloutActivityStore(paths);

        var sessions = store.LoadActiveSessions(DateTimeOffset.Parse("2026-06-03T09:33:00Z"));

        var session = Assert.Single(sessions);
        Assert.Equal("codex-thread-1", session.SessionId);
        Assert.Equal("项目任务测试", session.DisplayName);
        Assert.Equal(@"D:\workspace\datespace", session.WorkingDirectory);
        Assert.Equal("vscode-project", session.Source);
        Assert.Equal(CodexLightState.Red, session.State);
        Assert.Equal("RolloutTaskStarted", session.Event);
    }

    [Fact]
    public void LoadActiveSessionsSkipsCompletedTurns()
    {
        var paths = new CodexPaths(CreateTempRoot());
        WriteRollout(paths, "thread-1", """
        {"timestamp":"2026-06-03T09:32:00Z","type":"session_meta","payload":{"id":"thread-1","cwd":"D:\\workspace\\datespace","source":"vscode"}}
        {"timestamp":"2026-06-03T09:32:01Z","type":"event_msg","payload":{"type":"user_message","message":"项目任务测试\n"}}
        {"timestamp":"2026-06-03T09:32:02Z","type":"event_msg","payload":{"type":"task_started","turn_id":"turn-1","started_at":1780479122}}
        {"timestamp":"2026-06-03T09:33:02Z","type":"event_msg","payload":{"type":"task_complete","turn_id":"turn-1"}}
        """);

        var store = new CodexRolloutActivityStore(paths);

        var sessions = store.LoadActiveSessions(DateTimeOffset.Parse("2026-06-03T09:34:00Z"));

        Assert.Empty(sessions);
    }

    [Fact]
    public void LoadActiveSessionsReadsOpenRolloutFiles()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var path = WriteRollout(paths, "thread-1", """
        {"timestamp":"2026-06-03T09:32:00Z","type":"session_meta","payload":{"id":"thread-1","cwd":"D:\\workspace\\datespace","source":"vscode"}}
        {"timestamp":"2026-06-03T09:32:01Z","type":"event_msg","payload":{"type":"user_message","message":"椤圭洰浠诲姟娴嬭瘯\n"}}
        {"timestamp":"2026-06-03T09:32:02Z","type":"event_msg","payload":{"type":"task_started","turn_id":"turn-1","started_at":1780479122}}
        """);

        using var writerHandle = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var store = new CodexRolloutActivityStore(paths);

        var sessions = store.LoadActiveSessions(DateTimeOffset.Parse("2026-06-03T09:33:00Z"));

        var session = Assert.Single(sessions);
        Assert.Equal("codex-thread-1", session.SessionId);
        Assert.Equal(CodexLightState.Red, session.State);
    }

    [Fact]
    public void LoadActiveSessionsInfersActiveTurnFromRecentTurnContextWhenStartIsOutsideTail()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var largeOutput = JsonSerializer.Serialize(new
        {
            timestamp = "2026-06-03T09:32:30Z",
            type = "response_item",
            payload = new
            {
                type = "message",
                content = new[]
                {
                    new
                    {
                        type = "output_text",
                        text = new string('x', 4 * 1024 * 1024 + 512)
                    }
                }
            }
        });
        WriteRollout(paths, "thread-1", string.Join("\n", new[]
        {
            """{"timestamp":"2026-06-03T09:32:00Z","type":"session_meta","payload":{"id":"thread-1","cwd":"D:\\workspace\\datespace","source":"vscode"}}""",
            """{"timestamp":"2026-06-03T09:32:02Z","type":"event_msg","payload":{"type":"task_started","turn_id":"turn-1","started_at":1780479122}}""",
            largeOutput,
            """{"timestamp":"2026-06-03T09:40:00Z","type":"turn_context","payload":{"turn_id":"turn-1","cwd":"D:\\workspace\\datespace"}}"""
        }));

        var store = new CodexRolloutActivityStore(paths);

        var sessions = store.LoadActiveSessions(DateTimeOffset.Parse("2026-06-03T09:41:00Z"));

        var session = Assert.Single(sessions);
        Assert.Equal("codex-thread-1", session.SessionId);
        Assert.Equal(@"D:\workspace\datespace", session.WorkingDirectory);
        Assert.Equal(CodexLightState.Red, session.State);
    }

    [Fact]
    public void SessionStatusStoreMergesActiveProjectRollouts()
    {
        var paths = new CodexPaths(CreateTempRoot());
        WriteRollout(paths, "thread-1", """
        {"timestamp":"2026-06-03T09:32:00Z","type":"session_meta","payload":{"id":"thread-1","cwd":"D:\\workspace\\datespace","source":"vscode"}}
        {"timestamp":"2026-06-03T09:32:01Z","type":"event_msg","payload":{"type":"user_message","message":"项目任务测试\n"}}
        {"timestamp":"2026-06-03T09:32:02Z","type":"event_msg","payload":{"type":"task_started","turn_id":"turn-1","started_at":1780479122}}
        """);

        var store = new SessionStatusStore(paths, _ => false);

        var sessions = store.LoadVisibleSessions(DateTimeOffset.Parse("2026-06-03T09:33:00Z"));

        var session = Assert.Single(sessions);
        Assert.Equal("codex-thread-1", session.SessionId);
        Assert.Equal(CodexLightState.Red, session.State);
    }

    private static string WriteRollout(CodexPaths paths, string threadId, string content)
    {
        var directory = Path.Combine(paths.CodexDirectory, "sessions", "2026", "06", "03");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"rollout-2026-06-03T09-32-00-{threadId}.jsonl");
        File.WriteAllText(path, content.ReplaceLineEndings("\n").Trim() + "\n");
        return path;
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
