using System.Globalization;
using TimeTracker.Core;
using TimeTracker.Core.Models;
using TimeTracker.Core.Services;

var builder = WebApplication.CreateBuilder(args);

AppPaths.SetAppDir(ResolveAppDirectory(builder));
builder.WebHost.UseUrls($"http://{AppConstants.DashboardHost}:{AppConstants.DashboardPort}");

builder.Services.AddSingleton<ActivityRepository>(sp =>
    ActivityRepository.FromAppPaths(sp.GetRequiredService<ILogger<ActivityRepository>>()));
builder.Services.AddSingleton<SettingsStore>(sp =>
    SettingsStore.FromAppPaths(sp.GetRequiredService<ILogger<SettingsStore>>()));
builder.Services.AddSingleton<ActivityQueryService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = AppConstants.AppDisplayName }));

app.MapGet("/api/meta", () => Results.Ok(new
{
    categories = AppCategories.All,
    dashboardPort = AppConstants.DashboardPort,
}));

app.MapGet("/api/dates", (ActivityQueryService queries) =>
{
    var dates = queries.GetAvailableDates()
        .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        .ToList();
    return Results.Ok(dates);
});

app.MapGet("/api/activity", (ActivityQueryService queries, string? date) =>
{
    if (string.IsNullOrWhiteSpace(date) ||
        !DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
    {
        return Results.BadRequest(new { error = "Parâmetro 'date' inválido. Use yyyy-MM-dd." });
    }

    var records = queries.LoadByDate(parsedDate);
    var totalSeconds = records.Sum(record => record.DurationSeconds);
    var topApp = records
        .GroupBy(record => record.DisplayName)
        .Select(group => new { Name = group.Key, Total = group.Sum(item => item.DurationSeconds) })
        .OrderByDescending(item => item.Total)
        .FirstOrDefault();

    return Results.Ok(new
    {
        date = parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        hasData = records.Count > 0,
        records,
        summary = new
        {
            totalSeconds,
            sessionCount = records.Count,
            topApp = topApp?.Name,
        },
    });
});

app.MapGet("/api/apps", (ActivityQueryService queries) => Results.Ok(queries.GetAppsWithSettings()));

app.MapGet("/api/settings", (SettingsStore settings) => Results.Ok(settings.GetAppSettings()));

app.MapPut("/api/settings/{appName}", (
    string appName,
    AppSettingUpdateRequest request,
    SettingsStore settings) =>
{
    if (string.IsNullOrWhiteSpace(appName))
    {
        return Results.BadRequest(new { error = "Nome do app inválido." });
    }

    var saved = settings.UpdateAppSetting(
        appName,
        request.DisplayName ?? appName,
        request.HexColor,
        request.Category);

    return saved
        ? Results.Ok(new { saved = 1 })
        : Results.Problem("Erro ao salvar configurações.");
});

app.MapPost("/api/settings/batch", (BatchSettingsRequest request, SettingsStore settings) =>
{
    var updates = request.Changes?
        .Where(change => !string.IsNullOrWhiteSpace(change.AppName))
        .Select(change => new AppSettingUpdate
        {
            AppName = change.AppName,
            DisplayName = change.DisplayName ?? change.AppName,
            HexColor = change.HexColor,
            Category = change.Category,
        })
        .ToList() ?? [];

    var saved = settings.UpdateChangedSettings(updates);
    return Results.Ok(new { saved });
});

app.MapFallbackToFile("index.html");

app.Run();

static string ResolveAppDirectory(WebApplicationBuilder webBuilder)
{
    var contentRoot = webBuilder.Environment.ContentRootPath;
    var directory = new DirectoryInfo(contentRoot);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "TimeTracker.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return contentRoot;
}

internal sealed class AppSettingUpdateRequest
{
    public string? DisplayName { get; init; }

    public string? HexColor { get; init; }

    public string? Category { get; init; }
}

internal sealed class BatchSettingsRequest
{
    public List<AppSettingUpdateRequestItem>? Changes { get; init; }
}

internal sealed class AppSettingUpdateRequestItem
{
    public string AppName { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? HexColor { get; init; }

    public string? Category { get; init; }
}
