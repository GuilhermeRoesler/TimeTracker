using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace TimeTracker.Core.Services;

public sealed class ActivityRepository
{
    private readonly string _dbPath;
    private readonly ILogger<ActivityRepository> _logger;

    public ActivityRepository(string dbPath, ILogger<ActivityRepository> logger)
    {
        _dbPath = dbPath;
        _logger = logger;
        InitializeDatabase();
    }

    public static ActivityRepository FromAppPaths(ILogger<ActivityRepository> logger)
        => new(AppPaths.GetDbPath(), logger);

    private void InitializeDatabase()
    {
        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            command.ExecuteNonQuery();

            command.CommandText = """
                CREATE TABLE IF NOT EXISTS activity_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    app_name TEXT NOT NULL,
                    window_title TEXT,
                    start_time TIMESTAMP NOT NULL,
                    end_time TIMESTAMP,
                    duration_seconds REAL
                )
                """;
            command.ExecuteNonQuery();

            _logger.LogInformation("Banco de dados inicializado com sucesso.");
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Erro ao inicializar banco de dados.");
        }
    }

    public void SaveActivity(string appName, string? windowTitle, double startUnix, double endUnix)
    {
        if (endUnix <= startUnix)
        {
            return;
        }

        try
        {
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            var currentStart = startUnix;
            while (currentStart < endUnix)
            {
                var nextHourUnix = GetNextHourPartitionUnix(currentStart);
                var currentEnd = Math.Min(endUnix, nextHourUnix);
                var duration = currentEnd - currentStart;

                if (duration >= 1.0)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO activity_log (app_name, window_title, start_time, end_time, duration_seconds)
                        VALUES ($app, $title, $start, $end, $duration)
                        """;
                    insert.Parameters.AddWithValue("$app", appName);
                    insert.Parameters.AddWithValue("$title", windowTitle ?? string.Empty);
                    insert.Parameters.AddWithValue("$start", FormatTimestamp(currentStart));
                    insert.Parameters.AddWithValue("$end", FormatTimestamp(currentEnd));
                    insert.Parameters.AddWithValue("$duration", duration);
                    insert.ExecuteNonQuery();
                }

                currentStart = currentEnd;
            }

            transaction.Commit();
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Erro ao salvar atividade.");
        }
    }

    public IReadOnlyList<string> GetAllApps()
    {
        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT app_name FROM activity_log ORDER BY app_name";

            var apps = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                apps.Add(reader.GetString(0));
            }

            return apps;
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Erro ao listar apps.");
            return Array.Empty<string>();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }

    private static string FormatTimestamp(double unixSeconds)
        => FromUnixLocal(unixSeconds).ToString("yyyy-MM-dd HH:mm:ss.ffffff");

    private static double GetNextHourPartitionUnix(double unixSeconds)
    {
        var startLocal = FromUnixLocal(unixSeconds);
        var nextHour = startLocal.AddHours(1);
        nextHour = new DateTime(
            nextHour.Year,
            nextHour.Month,
            nextHour.Day,
            nextHour.Hour,
            0,
            0,
            nextHour.Kind);
        return new DateTimeOffset(nextHour).ToUnixTimeSeconds();
    }

    private static DateTime FromUnixLocal(double unixSeconds)
    {
        var wholeSeconds = (long)unixSeconds;
        var fractional = unixSeconds - wholeSeconds;
        return DateTimeOffset.FromUnixTimeSeconds(wholeSeconds).LocalDateTime.AddSeconds(fractional);
    }
}
