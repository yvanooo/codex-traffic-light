using System.IO;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.App;

public sealed class SessionStatusDirectoryWatcher : IDisposable
{
    private readonly SessionStatusStore _store;
    private readonly Func<bool> _includeEndedSessions;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly System.Timers.Timer _refreshTimer;

    public SessionStatusDirectoryWatcher(CodexPaths paths, SessionStatusStore store, Func<bool>? includeEndedSessions = null)
    {
        _store = store;
        _includeEndedSessions = includeEndedSessions ?? (() => false);
        Directory.CreateDirectory(paths.SessionDirectory);

        _watcher = new FileSystemWatcher(paths.SessionDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;

        _debounce = new System.Timers.Timer(120) { AutoReset = false };
        _debounce.Elapsed += (_, _) => SessionsChanged?.Invoke(LoadSessions());

        _refreshTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5)) { AutoReset = true };
        _refreshTimer.Elapsed += (_, _) => SessionsChanged?.Invoke(LoadSessions());
        _refreshTimer.Start();
    }

    public event Action<IReadOnlyList<CodexSessionStatus>>? SessionsChanged;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private IReadOnlyList<CodexSessionStatus> LoadSessions()
    {
        return _store.LoadSessions(_includeEndedSessions());
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
        _refreshTimer.Dispose();
    }
}
