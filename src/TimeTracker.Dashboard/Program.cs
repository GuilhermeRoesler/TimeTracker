using TimeTracker.Core;
using TimeTracker.Core.Services;

var builder = WebApplication.CreateBuilder(args);

AppPaths.SetAppDir(ResolveAppDirectory(builder));
builder.Services.AddSingleton<ActivityRepository>(sp =>
    ActivityRepository.FromAppPaths(sp.GetRequiredService<ILogger<ActivityRepository>>()));
builder.Services.AddSingleton<SettingsStore>(sp =>
    SettingsStore.FromAppPaths(sp.GetRequiredService<ILogger<SettingsStore>>()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = AppConstants.AppDisplayName }));

app.MapGet("/api/dates", (ActivityRepository repository) =>
{
    // Placeholder: será implementado na Fase 2 com agregação por data.
    var apps = repository.GetAllApps();
    return Results.Ok(new { dates = Array.Empty<string>(), appsCount = apps.Count });
});

app.MapGet("/api/activity", (ActivityRepository repository, string? date) =>
{
    // Placeholder: retorno vazio até a camada de consulta ser portada do dashboard Python.
    _ = repository;
    _ = date;
    return Results.Ok(Array.Empty<object>());
});

app.MapGet("/api/settings", (SettingsStore settings) => Results.Ok(settings.GetAppSettings()));

app.MapFallbackToFile("index.html");

app.Run();

static string ResolveAppDirectory(WebApplicationBuilder webBuilder)
{
    var contentRoot = webBuilder.Environment.ContentRootPath;
    var directory = new DirectoryInfo(contentRoot);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "TimeTracker.sln")) ||
            File.Exists(Path.Combine(directory.FullName, "main.py")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return contentRoot;
}
