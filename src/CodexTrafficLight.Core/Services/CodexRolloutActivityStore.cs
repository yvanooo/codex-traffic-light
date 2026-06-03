using System.Text;
using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class CodexRolloutActivityStore
{
    private const int TailBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan ActiveRetention = TimeSpan.FromHours(2);
    private readonly CodexPaths _paths;

    public CodexRolloutActivityStore(CodexPaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<CodexSessionStatus> LoadActiveSessions(DateTimeOffset? now = null)
    {
        if (!Directory.Exists(_paths.RolloutDirectory))
        {
            return Array.Empty<CodexSessionStatus>();
        }

        var current = now ?? DateTimeOffset.Now;
        var sessions = new List<CodexSessionStatus>();
        foreach (var path in Directory.EnumerateFiles(_paths.RolloutDirectory, "rollout-*.jsonl", SearchOption.AllDirectories))
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (current - new DateTimeOffset(lastWrite) > ActiveRetention)
                {
                    continue;
                }

                var session = TryReadActiveSession(path, current);
                if (session is not null)
                {
                    sessions.Add(session);
                }
            }
            catch
            {
                // Ignore a single broken rollout file; hook sessions should still render.
            }
        }

        return sessions;
    }

    private static CodexSessionStatus? TryReadActiveSession(string path, DateTimeOffset now)
    {
        var metadata = ReadMetadata(path);
        if (string.IsNullOrWhiteSpace(metadata.SessionId))
        {
            return null;
        }

        var tail = ReadTail(path);
        string? latestActiveTurnId = null;
        DateTimeOffset? latestActiveAt = null;
        var completedTurns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var abortedTurns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayName = string.Empty;
        var workingDirectory = metadata.WorkingDirectory;

        foreach (var line in tail.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var timestamp = TryReadTimestamp(root) ?? latestActiveAt ?? now;
            var type = ReadString(root, "type");
            if (type == "turn_context" && root.TryGetProperty("payload", out var turnPayload))
            {
                workingDirectory = ReadString(turnPayload, "cwd") ?? workingDirectory;
                var contextTurnId = ReadString(turnPayload, "turn_id");
                if (!string.IsNullOrWhiteSpace(contextTurnId))
                {
                    latestActiveTurnId = contextTurnId;
                    latestActiveAt = timestamp;
                }

                continue;
            }

            if (type != "event_msg" || !root.TryGetProperty("payload", out var payload))
            {
                continue;
            }

            var eventType = ReadString(payload, "type");
            switch (eventType)
            {
                case "user_message":
                    displayName = NormalizeDisplayName(ReadString(payload, "message"));
                    break;
                case "task_started":
                    latestActiveTurnId = ReadString(payload, "turn_id");
                    latestActiveAt = timestamp;
                    break;
                case "task_complete":
                    AddTurn(completedTurns, payload);
                    break;
                case "turn_aborted":
                    AddTurn(abortedTurns, payload);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(latestActiveTurnId) ||
            completedTurns.Contains(latestActiveTurnId) ||
            abortedTurns.Contains(latestActiveTurnId))
        {
            return null;
        }

        return new CodexSessionStatus
        {
            SessionId = "codex-" + metadata.SessionId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Codex project task" : displayName,
            WorkingDirectory = workingDirectory,
            Source = "vscode-project",
            State = CodexLightState.Red,
            Event = "RolloutTaskStarted",
            UpdatedAt = latestActiveAt ?? now
        };
    }

    private static RolloutMetadata ReadMetadata(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (ReadString(root, "type") != "session_meta" || !root.TryGetProperty("payload", out var payload))
                {
                    continue;
                }

                return new RolloutMetadata(
                    ReadString(payload, "id") ?? string.Empty,
                    ReadString(payload, "cwd") ?? string.Empty);
            }
        }
        catch
        {
        }

        return new RolloutMetadata(string.Empty, string.Empty);
    }

    private static string ReadTail(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytesToRead = (int)Math.Min(TailBytes, stream.Length);
        stream.Seek(-bytesToRead, SeekOrigin.End);
        var buffer = new byte[bytesToRead];
        var read = stream.Read(buffer, 0, bytesToRead);
        var text = Encoding.UTF8.GetString(buffer, 0, read);

        if (stream.Length <= TailBytes)
        {
            return text;
        }

        var firstLineBreak = text.IndexOf('\n');
        return firstLineBreak >= 0 ? text[(firstLineBreak + 1)..] : string.Empty;
    }

    private static void AddTurn(HashSet<string> turns, JsonElement payload)
    {
        var turnId = ReadString(payload, "turn_id");
        if (!string.IsNullOrWhiteSpace(turnId))
        {
            turns.Add(turnId);
        }
    }

    private static DateTimeOffset? TryReadTimestamp(JsonElement root)
    {
        var raw = ReadString(root, "timestamp");
        return DateTimeOffset.TryParse(raw, out var timestamp) ? timestamp : null;
    }

    private static string NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 36 ? normalized[..36] : normalized;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private sealed record RolloutMetadata(string SessionId, string WorkingDirectory);
}
