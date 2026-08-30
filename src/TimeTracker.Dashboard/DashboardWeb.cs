using System.Globalization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TimeTracker.Core;
using TimeTracker.Core.Models;
using TimeTracker.Core.Services;

namespace TimeTracker.Dashboard;

public static class DashboardWeb
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ActivityRepository>(sp =>
            ActivityRepository.FromAppPaths(sp.GetRequiredService<ILogger<ActivityRepository>>()));
        services.TryAddSingleton<SettingsStore>(sp =>
            SettingsStore.FromAppPaths(sp.GetRequiredService<ILogger<SettingsStore>>()));
        services.TryAddSingleton<ActivityQueryService>();
        services.TryAddSingleton<UpdateAvailabilityState>();
        return services;
    }

    public static WebApplication CreateStandalone(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        AppPaths.InitializeFromEnvironmentOrDefaults(builder.Environment.ContentRootPath);
        builder.WebHost.UseUrls($"http://{AppConstants.DashboardHost}:{AppConstants.DashboardPort}");
        builder.Services.AddDashboardServices();

        var app = builder.Build();
        app.MapDashboard();
        return app;
    }

    public static WebApplication MapDashboard(this WebApplication app)
    {
        // Evita WebView2/browser reutilizar JS/CSS antigos após update do wwwroot.
        var staticFiles = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
            },
        };

        app.UseDefaultFiles();
        app.UseStaticFiles(staticFiles);

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

        app.MapGet("/api/update", (UpdateAvailabilityState updateState) =>
            Results.Ok(updateState.ToApiResponse()));

        app.MapPost("/api/update/apply", async (UpdateAvailabilityState updateState, CancellationToken cancellationToken) =>
        {
            if (!updateState.UpdatesEnabled)
            {
                return Results.Json(
                    new { error = "Atualizações automáticas não estão disponíveis neste modo." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (!updateState.Available)
            {
                return Results.Json(
                    new { error = "Nenhuma atualização pendente." },
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (updateState.ApplyHandler is null)
            {
                return Results.Json(
                    new { error = "Instalação de atualização indisponível." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (updateState.Installing)
            {
                return Results.Ok(new { started = true, alreadyRunning = true });
            }

            var result = await updateState.ApplyHandler(cancellationToken);
            if (!result.Accepted)
            {
                return Results.Json(
                    new { error = result.ErrorMessage ?? "Não foi possível iniciar a atualização." },
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(new { started = true });
        });

        app.MapFallbackToFile("index.html");
        return app;
    }
}

public sealed class AppSettingUpdateRequest
{
    public string? DisplayName { get; init; }

    public string? HexColor { get; init; }

    public string? Category { get; init; }
}

public sealed class BatchSettingsRequest
{
    public List<AppSettingUpdateRequestItem>? Changes { get; init; }
}

public sealed class AppSettingUpdateRequestItem
{
    public string AppName { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? HexColor { get; init; }

    public string? Category { get; init; }
}
