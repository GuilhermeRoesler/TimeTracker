using Microsoft.Extensions.Logging.Abstractions;
using TimeTracker.Core.Services;

namespace TimeTracker.Core.Tests;

public class ActivityRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ActivityRepository _repository;

    public ActivityRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"timetracker-test-{Guid.NewGuid():N}.db");
        _repository = new ActivityRepository(_dbPath, NullLogger<ActivityRepository>.Instance);
    }

    public void Dispose() => TestDatabaseHelper.DeleteDatabase(_dbPath);

    [Fact]
    public void SaveActivity_discards_sessions_shorter_than_one_second()
    {
        var start = ToUnix(DateTime.Today.AddHours(10));
        _repository.SaveActivity("notepad.exe", "Untitled", start, start + 0.5);

        Assert.Empty(_repository.GetAllActivities());
    }

    [Fact]
    public void SaveActivity_partitions_at_hour_boundary()
    {
        var start = ToUnix(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Local));
        var end = ToUnix(new DateTime(2026, 1, 15, 11, 30, 0, DateTimeKind.Local));

        _repository.SaveActivity("code.exe", "main.cs", start, end);

        var rows = _repository.GetAllActivities();
        Assert.Equal(2, rows.Count);
        Assert.Equal(1800, rows[0].DurationSeconds, precision: 1);
        Assert.Equal(1800, rows[1].DurationSeconds, precision: 1);
    }

    [Fact]
    public void GetAllApps_returns_distinct_sorted_names()
    {
        var start = ToUnix(DateTime.Today.AddHours(9));
        _repository.SaveActivity("z.exe", "Z", start, start + 60);
        _repository.SaveActivity("a.exe", "A", start + 120, start + 180);

        var apps = _repository.GetAllApps();
        Assert.Equal(["a.exe", "z.exe"], apps);
    }

    private static double ToUnix(DateTime localDateTime)
        => new DateTimeOffset(localDateTime).ToUnixTimeSeconds();
}
