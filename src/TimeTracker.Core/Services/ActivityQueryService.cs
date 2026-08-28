using TimeTracker.Core;
using TimeTracker.Core.Models;

namespace TimeTracker.Core.Services;

public sealed class ActivityQueryService
{
    private readonly ActivityRepository _repository;
    private readonly SettingsStore _settingsStore;

    public ActivityQueryService(ActivityRepository repository, SettingsStore settingsStore)
    {
        _repository = repository;
        _settingsStore = settingsStore;
    }

    public IReadOnlyList<ActivityEntry> LoadAllEnriched()
    {
        var settings = _settingsStore.GetAppSettings();
        return _repository.GetAllActivities()
            .Select(row => Enrich(row, settings))
            .OrderByDescending(entry => entry.StartTime)
            .ToList();
    }

    public IReadOnlyList<DateOnly> GetAvailableDates()
        => LoadAllEnriched()
            .Select(entry => entry.Date)
            .Distinct()
            .OrderDescending()
            .ToList();

    public IReadOnlyList<ActivityEntry> LoadByDate(DateOnly date)
        => LoadAllEnriched()
            .Where(entry => entry.Date == date)
            .ToList();

    public IReadOnlyList<AppSettingsView> GetAppsWithSettings()
    {
        var settings = _settingsStore.GetAppSettings();
        return _repository.GetAllApps()
            .Select(appName =>
            {
                settings.TryGetValue(appName, out var entry);
                return new AppSettingsView
                {
                    AppName = appName,
                    DisplayName = entry?.DisplayName ?? appName,
                    HexColor = entry?.HexColor,
                    Category = AppCategories.Normalize(entry?.Category),
                };
            })
            .OrderBy(view => view.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ActivityEntry Enrich(ActivityLogRow row, IReadOnlyDictionary<string, AppSettingEntry> settings)
    {
        settings.TryGetValue(row.AppName, out var appSetting);

        var displayName = appSetting?.DisplayName ?? row.AppName;
        var category = AppCategories.Normalize(appSetting?.Category);

        return new ActivityEntry
        {
            Id = row.Id,
            AppName = row.AppName,
            WindowTitle = row.WindowTitle,
            StartTime = row.StartTime,
            EndTime = row.EndTime,
            DurationSeconds = row.DurationSeconds,
            DisplayName = displayName,
            HexColor = appSetting?.HexColor,
            Category = category,
            Date = DateOnly.FromDateTime(row.StartTime),
            Hour = row.StartTime.Hour,
        };
    }
}

public sealed class AppSettingsView
{
    public string AppName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? HexColor { get; init; }

    public string Category { get; init; } = "Sem Categoria";
}
