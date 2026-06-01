using System.IO;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.App;

public sealed class StatusFileWatcher : IDisposable
{
    private readonly StatusFileStore _store;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;

    public StatusFileWatcher(CodexPaths paths, StatusFileStore store)
    {
        _store = store;
        Directory.CreateDirectory(paths.CodexDirectory);

        _watcher = new FileSystemWatcher(paths.CodexDirectory, Path.GetFileName(paths.StatusPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;

        _debounce = new System.Timers.Timer(120) { AutoReset = false };
        _debounce.Elapsed += (_, _) => StatusChanged?.Invoke(_store.Read());
    }

    public event Action<CodexStatus>? StatusChanged;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
