using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class StatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    public StatsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    public Dictionary<string, DailyStats> Load()
    {
        try
        {
            if (!File.Exists(_paths.StatsPath))
            {
                return new Dictionary<string, DailyStats>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, DailyStats>>(File.ReadAllText(_paths.StatsPath), JsonOptions)
                ?? new Dictionary<string, DailyStats>();
        }
        catch
        {
            return new Dictionary<string, DailyStats>();
        }
    }

    public void RecordStateChange(
        CodexLightState newState,
        CodexLightState previousState,
        DateTimeOffset? redStartedAt,
        DateOnly? localDay = null,
        DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.Now;
        var key = (localDay ?? DateOnly.FromDateTime(currentTime.LocalDateTime)).ToString("yyyy-MM-dd");
        var all = Load();
        all.TryGetValue(key, out var current);
        current ??= new DailyStats();

        var next = newState switch
        {
            CodexLightState.Red => current with { RedCount = current.RedCount + 1 },
            CodexLightState.Green => current with
            {
                GreenCount = current.GreenCount + 1,
                RedDurationMs = current.RedDurationMs + CalculateRedDuration(previousState, redStartedAt, currentTime)
            },
            _ => current
        };

        all[key] = next;
        Save(all);
    }

    private static long CalculateRedDuration(CodexLightState previousState, DateTimeOffset? redStartedAt, DateTimeOffset now)
    {
        if (previousState != CodexLightState.Red || redStartedAt is null)
        {
            return 0;
        }

        var duration = now - redStartedAt.Value;
        return duration < TimeSpan.Zero ? 0 : (long)duration.TotalMilliseconds;
    }

    private void Save(Dictionary<string, DailyStats> stats)
    {
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.StatsPath, JsonSerializer.Serialize(stats, JsonOptions));
    }
}
