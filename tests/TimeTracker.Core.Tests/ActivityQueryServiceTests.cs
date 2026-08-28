using Microsoft.Extensions.Logging.Abstractions;
using TimeTracker.Core.Services;

namespace TimeTracker.Core.Tests;

public class ActivityQueryServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _settingsPath;
    private readonly ActivityQueryService _queries;

    public ActivityQueryServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"timetracker-query-{Guid.NewGuid():N}.db");
        _settingsPath = Path.Combine(Path.GetTempPath(), $"timetracker-query-settings-{Guid.NewGuid():N}.json");

        var repository = new ActivityRepository(_dbPath, NullLogger<ActivityRepository>.Instance);
        var settings = new SettingsStore(_settingsPath, NullLogger<SettingsStore>.Instance);
        settings.UpdateAppSetting("code.exe", "VS Code", "#007ACC", "Desenvolvimento");

        var day = new DateTime(2026, 3, 10, 14, 0, 0, DateTimeKind.Local);
        var start = new DateTimeOffset(day).ToUnixTimeSeconds();
        repository.SaveActivity("code.exe", "Program.cs", start, start + 120);
        repository.SaveActivity("chrome.exe", "Docs", start + 300, start + 420);

        _queries = new ActivityQueryService(repository, settings);
    }

    public void Dispose()
    {
        TestDatabaseHelper.DeleteDatabase(_dbPath);
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    [Fact]
    public void LoadByDate_returns_only_matching_day()
    {
        var records = _queries.LoadByDate(new DateOnly(2026, 3, 10));
        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal(new DateOnly(2026, 3, 10), record.Date));
    }

    [Fact]
    public void LoadByDate_enriches_display_name_and_category()
    {
        var record = _queries.LoadByDate(new DateOnly(2026, 3, 10))
            .Single(entry => entry.AppName == "code.exe");

        Assert.Equal("VS Code", record.DisplayName);
        Assert.Equal("Desenvolvimento", record.Category);
        Assert.Equal("#007ACC", record.HexColor);
    }

    [Fact]
    public void GetAvailableDates_returns_descending_unique_dates()
    {
        var dates = _queries.GetAvailableDates();
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 3, 10), dates[0]);
    }
}
