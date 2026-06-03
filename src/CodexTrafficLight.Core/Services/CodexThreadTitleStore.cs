using Microsoft.Data.Sqlite;

namespace CodexTrafficLight.Core.Services;

public sealed class CodexThreadTitleStore
{
    private readonly CodexPaths _paths;

    public CodexThreadTitleStore(CodexPaths paths)
    {
        _paths = paths;
    }

    public string? GetTitle(string sessionId)
    {
        var threadId = NormalizeThreadId(sessionId);
        if (string.IsNullOrWhiteSpace(threadId) || !File.Exists(_paths.StateDatabasePath))
        {
            return null;
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _paths.StateDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT title FROM threads WHERE id = $id LIMIT 1";
            command.Parameters.AddWithValue("$id", threadId);
            var value = command.ExecuteScalar();
            return NormalizeTitle(value as string);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeThreadId(string sessionId)
    {
        const string prefix = "codex-";
        return sessionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? sessionId[prefix.Length..]
            : sessionId;
    }

    private static string? NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
