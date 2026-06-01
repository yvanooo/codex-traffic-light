using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class StatusFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);
    private readonly CodexPaths _paths;

    public StatusFileStore(CodexPaths paths)
    {
        _paths = paths;
    }

    public CodexStatus Read()
    {
        try
        {
            if (!File.Exists(_paths.StatusPath))
            {
                return new CodexStatus(CodexLightState.Unknown, "missing", DateTimeOffset.Now);
            }

            var json = File.ReadAllText(_paths.StatusPath);
            return JsonSerializer.Deserialize<CodexStatus>(json, JsonOptions)
                ?? new CodexStatus(CodexLightState.Unknown, "invalid", DateTimeOffset.Now);
        }
        catch
        {
            return new CodexStatus(CodexLightState.Unknown, "error", DateTimeOffset.Now);
        }
    }

    public void Write(CodexStatus status)
    {
        _paths.EnsureCodexDirectory();
        var tempPath = _paths.StatusPath + ".tmp";
        var json = JsonSerializer.Serialize(status, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _paths.StatusPath, overwrite: true);
    }

    public void Write(CodexLightState state, string eventName)
    {
        Write(new CodexStatus(state, eventName, DateTimeOffset.Now));
    }
}
