using System.Text.Json;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

public sealed class CodexHookInstallerTests
{
    [Fact]
    public void InstallPreservesUnrelatedHooks()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        paths.EnsureCodexDirectory();
        File.WriteAllText(paths.HooksPath, """
        {
          "hooks": {
            "PreToolUse": [
              {
                "matcher": "Bash",
                "hooks": [
                  {
                    "type": "command",
                    "command": "echo keep-me"
                  }
                ]
              }
            ]
          }
        }
        """);

        new CodexHookInstaller(paths).InstallOrUpdate();
        var json = File.ReadAllText(paths.HooksPath);

        Assert.Contains("echo keep-me", json);
        Assert.Contains("codex_traffic_light_write_status.ps1", json);
        Assert.Contains("UserPromptSubmit", json);
        Assert.Contains("PermissionRequest", json);
        Assert.Contains("Stop", json);
        Assert.Contains("SessionStart", json);
        Assert.True(File.Exists(paths.HookScriptPath));
    }

    [Fact]
    public void InstallIsIdempotent()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        var installer = new CodexHookInstaller(paths);

        installer.InstallOrUpdate();
        var first = File.ReadAllText(paths.HooksPath);
        installer.InstallOrUpdate();
        var second = File.ReadAllText(paths.HooksPath);

        Assert.Equal(NormalizeJson(first), NormalizeJson(second));
    }

    [Fact]
    public void InvalidHooksJsonIsBackedUp()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        paths.EnsureCodexDirectory();
        File.WriteAllText(paths.HooksPath, "{bad-json");

        new CodexHookInstaller(paths).InstallOrUpdate();

        Assert.NotEmpty(Directory.GetFiles(paths.CodexDirectory, "hooks.json.invalid-*.bak"));
        Assert.Contains("codex_traffic_light_write_status.ps1", File.ReadAllText(paths.HooksPath));
    }

    [Fact]
    public void HookScriptWritesDiagnosticsAndPerSessionStatusFiles()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);

        new CodexHookInstaller(paths).InstallOrUpdate();

        var script = File.ReadAllText(paths.HookScriptPath);
        Assert.Contains("diagnostics", script);
        Assert.Contains("Get-CimInstance Win32_Process", script);
        Assert.Contains("sessions", script);
        Assert.Contains(".json.tmp", script);
        Assert.Contains("Move-Item", script);
        Assert.Contains("CODEX_TRAFFIC_LIGHT_SESSION_ID", script);
        Assert.Contains("[Console]::In.ReadToEnd()", script);
        Assert.Contains("[Console]::InputEncoding", script);
        Assert.Contains("TrimStart([char]0xFEFF)", script);
        Assert.Contains("session_id", script);
        Assert.Contains("prompt", script);
        Assert.Contains("rawHookInput", script);
    }

    private static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
