using Microsoft.Data.Sqlite;

namespace TimeTracker.Core.Tests;

internal static class TestDatabaseHelper
{
    public static void DeleteDatabase(string dbPath)
    {
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var path = dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
