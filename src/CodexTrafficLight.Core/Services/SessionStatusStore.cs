using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class SessionStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);
    private static readonly TimeSpan GreenRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan YellowRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedRetention = TimeSpan.FromMinutes(10);
    private readonly CodexPaths _paths;
    private readonly Func<int, bool> _isProcessRunning;

    public SessionStatusStore(CodexPaths paths, Func<int, bool>? isProcessRunning = null)
    {
        _paths = paths;
        _isProcessRunning = isProcessRunning ?? IsProcessRunning;
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
        return LoadAllSessions()
            .Where(session => IsVisible(session, now ?? DateTimeOffset.Now, includeEnded))
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
            CodexLightState.Yellow => age <= YellowRetention || IsLiveCliWork(session),
            CodexLightState.Red => age <= RedRetention || IsLiveCliWork(session),
            _ => age <= RedRetention
        };
    }

    private bool IsLiveCliWork(CodexSessionStatus session)
    {
        return session.Source.Equals("cli", StringComparison.OrdinalIgnoreCase) &&
               session.ProcessId > 0 &&
               _isProcessRunning(session.ProcessId);
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
