using CodexTrafficLight.Core.Services;
using Microsoft.Data.Sqlite;

namespace CodexTrafficLight.Tests;

public sealed class CodexThreadTitleStoreTests
{
    [Fact]
    public void GetTitleReadsThreadTitleFromCodexStateDatabase()
    {
        var paths = new CodexPaths(CreateTempRoot());
        Directory.CreateDirectory(paths.CodexDirectory);
        using (var connection = new SqliteConnection($"Data Source={paths.StateDatabasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
            CREATE TABLE threads (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                preview TEXT NOT NULL,
                first_user_message TEXT NOT NULL
            );
            INSERT INTO threads (id, title, preview, first_user_message)
            VALUES ('thread-1', '阅读项目源码', '当前消息内容', '第一条消息内容');
            """;
            command.ExecuteNonQuery();
        }

        var store = new CodexThreadTitleStore(paths);

        Assert.Equal("阅读项目源码", store.GetTitle("codex-thread-1"));
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
