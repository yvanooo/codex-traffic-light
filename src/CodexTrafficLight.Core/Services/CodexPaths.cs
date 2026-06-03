namespace CodexTrafficLight.Core.Services;

public sealed class CodexPaths
{
    public CodexPaths(string? homeDirectory = null)
    {
        HomeDirectory = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : homeDirectory;

        var codexHome = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetEnvironmentVariable("CODEX_HOME")
            : null;

        CodexDirectory = string.IsNullOrWhiteSpace(codexHome)
            ? Path.Combine(HomeDirectory, ".codex")
            : codexHome;

        HooksPath = Path.Combine(CodexDirectory, "hooks.json");
        StatusPath = Path.Combine(CodexDirectory, "codex_traffic_light_state.json");
        SettingsPath = Path.Combine(CodexDirectory, "codex_traffic_light_settings.json");
        StatsPath = Path.Combine(CodexDirectory, "codex_traffic_light_stats.json");
        StateDatabasePath = Path.Combine(CodexDirectory, "state_5.sqlite");
        HookScriptDirectory = Path.Combine(CodexDirectory, "codex-traffic-light");
        HookScriptPath = Path.Combine(HookScriptDirectory, "codex_traffic_light_write_status.ps1");
        SessionDirectory = Path.Combine(HookScriptDirectory, "sessions");
        HookDiagnosticsDirectory = Path.Combine(HookScriptDirectory, "diagnostics");
        RolloutDirectory = Path.Combine(CodexDirectory, "sessions");
    }

    public string HomeDirectory { get; }
    public string CodexDirectory { get; }
    public string HooksPath { get; }
    public string StatusPath { get; }
    public string SettingsPath { get; }
    public string StatsPath { get; }
    public string StateDatabasePath { get; }
    public string HookScriptDirectory { get; }
    public string HookScriptPath { get; }
    public string SessionDirectory { get; }
    public string HookDiagnosticsDirectory { get; }
    public string RolloutDirectory { get; }

    public void EnsureCodexDirectory()
    {
        Directory.CreateDirectory(CodexDirectory);
    }
}
