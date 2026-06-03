using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class SessionStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);
    private static readonly TimeSpan GreenRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan YellowRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedRetention = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LiveCliWorkRetention = TimeSpan.FromHours(6);
    private static readonly TimeSpan LiveVsCodePluginWorkRetention = TimeSpan.FromHours(2);
    private readonly CodexPaths _paths;
    private readonly Func<int, bool> _isProcessRunning;
    private readonly Func<DateTimeOffset, IReadOnlyList<CodexSessionStatus>> _loadSupplementalSessions;

    public SessionStatusStore(
        CodexPaths paths,
        Func<int, bool>? isProcessRunning = null,
        Func<DateTimeOffset, IReadOnlyList<CodexSessionStatus>>? loadSupplementalSessions = null)
    {
        _paths = paths;
        _isProcessRunning = isProcessRunning ?? IsProcessRunning;
        _loadSupplementalSessions = loadSupplementalSessions ?? (current => new CodexRolloutActivityStore(paths).LoadActiveSessions(current));
    }

    public void Write(CodexSessionStatus status)
    {
        Directory.CreateDirectory(_paths.SessionDirectory);
        var path = GetSessionPath(status.SessionId);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(status, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    public IReadOnlyList<CodexSessionStatus> LoadVisibleSessions(DateTimeOffset? now = null)
    {
        return LoadSessions(includeEnded: false, now);
    }

    public IReadOnlyList<CodexSessionStatus> LoadSessions(bool includeEnded, DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.Now;
        return LoadAllSessions()
            .Concat(_loadSupplementalSessions(current))
            .Where(session => IsVisible(session, current, includeEnded))
            .GroupBy(GetSessionGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(session => session.UpdatedAt).First())
            .OrderBy(GetPriority)
            .ThenByDescending(session => session.UpdatedAt)
            .ToList();
    }

    public IReadOnlyList<CodexSessionStatus> LoadAllSessions()
    {
        if (!Directory.Exists(_paths.SessionDirectory))
        {
            return Array.Empty<CodexSessionStatus>();
        }

        var sessions = new List<CodexSessionStatus>();
        foreach (var path in Directory.EnumerateFiles(_paths.SessionDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(path);
                var session = JsonSerializer.Deserialize<CodexSessionStatus>(json, JsonOptions);
                if (session is not null && !string.IsNullOrWhiteSpace(session.SessionId))
                {
                    sessions.Add(session);
                }
            }
            catch
            {
                // Ignore a single broken session file; other CLI windows should still render.
            }
        }

        return sessions;
    }

    public void ClearEndedSessions(DateTimeOffset? now = null)
    {
        if (!Directory.Exists(_paths.SessionDirectory))
        {
            return;
        }

        var cutoff = (now ?? DateTimeOffset.Now) - GreenRetention;
        foreach (var session in LoadAllSessions().Where(session => session.State == CodexLightState.Green && session.UpdatedAt < cutoff))
        {
            var path = GetSessionPath(session.SessionId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static CodexLightState GetAggregateState(IEnumerable<CodexSessionStatus> sessions)
    {
        var list = sessions.ToList();
        if (list.Count == 0)
        {
            return CodexLightState.Unknown;
        }

        if (list.Any(session => session.State == CodexLightState.Yellow))
        {
            return CodexLightState.Yellow;
        }

        if (list.Any(session => session.State == CodexLightState.Red))
        {
            return CodexLightState.Red;
        }

        return list.All(session => session.State == CodexLightState.Green)
            ? CodexLightState.Green
            : CodexLightState.Unknown;
    }

    public static string GetCompletionProgressText(IEnumerable<CodexSessionStatus> sessions)
    {
        var list = sessions.ToList();
        var completed = list.Count(session => session.State == CodexLightState.Green);
        return $"{completed}/{list.Count}";
    }

    private string GetSessionPath(string sessionId)
    {
        var fileName = SanitizeSessionId(sessionId) + ".json";
        return Path.Combine(_paths.SessionDirectory, fileName);
    }

    private bool IsVisible(CodexSessionStatus session, DateTimeOffset now, bool includeEnded)
    {
        var age = now - session.UpdatedAt;
        if (session.State == CodexLightState.Green)
        {
            if (includeEnded)
            {
                return true;
            }

            return age <= GreenRetention;
        }

        return session.State switch
        {
            CodexLightState.Yellow => age <= YellowRetention || IsLiveWork(session, age),
            CodexLightState.Red => age <= RedRetention || IsLiveWork(session, age),
            _ => age <= RedRetention
        };
    }

    private bool IsLiveWork(CodexSessionStatus session, TimeSpan age)
    {
        if (session.Source.Equals("vscode-project", StringComparison.OrdinalIgnoreCase))
        {
            return age <= LiveVsCodePluginWorkRetention;
        }

        if (session.ProcessId <= 0 || !_isProcessRunning(session.ProcessId))
        {
            return false;
        }

        if (session.Source.Equals("cli", StringComparison.OrdinalIgnoreCase))
        {
            return age <= LiveCliWorkRetention;
        }

        if (session.Source.Equals("vscode-plugin", StringComparison.OrdinalIgnoreCase))
        {
            return age <= LiveVsCodePluginWorkRetention;
        }

        return false;
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int GetPriority(CodexSessionStatus session)
    {
        return session.State switch
        {
            CodexLightState.Yellow => 0,
            CodexLightState.Red => 1,
            CodexLightState.Green => 2,
            _ => 3
        };
    }

    private static string GetSessionGroupKey(CodexSessionStatus session)
    {
        if (IsWeakSessionIdentity(session.SessionId) && !string.IsNullOrWhiteSpace(session.WorkingDirectory))
        {
            return "weak:" + session.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return "session:" + session.SessionId;
    }

    private static bool IsWeakSessionIdentity(string sessionId)
    {
        return sessionId.StartsWith("pid-", StringComparison.OrdinalIgnoreCase) &&
               sessionId.Contains("unknownstart", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeSessionId(string sessionId)
    {
        var chars = sessionId
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }
}
