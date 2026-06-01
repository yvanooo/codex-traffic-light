using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    public AppSettingsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsPath), JsonOptions)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
