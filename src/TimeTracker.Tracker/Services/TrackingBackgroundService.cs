using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Services;

namespace TimeTracker.Tracker.Services;

internal sealed class TrackingBackgroundService : BackgroundService
{
    private readonly TrackingEngine _trackingEngine;
    private readonly ILogger<TrackingBackgroundService> _logger;

    public TrackingBackgroundService(TrackingEngine trackingEngine, ILogger<TrackingBackgroundService> logger)
    {
        _trackingEngine = trackingEngine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _trackingEngine.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Monitoramento encerrado.");
        }
    }
}

internal static class TrackerServiceCollectionExtensions
{
    public static IServiceCollection AddTrackerServices(this IServiceCollection services)
    {
        services.AddSingleton<IActiveWindowProvider, Win32.Win32ActiveWindowProvider>();
        services.AddSingleton<ActivityRepository>(sp =>
            ActivityRepository.FromAppPaths(sp.GetRequiredService<ILogger<ActivityRepository>>()));
        services.AddSingleton<SettingsStore>(sp =>
            SettingsStore.FromAppPaths(sp.GetRequiredService<ILogger<SettingsStore>>()));
        services.AddSingleton<TrackingEngine>();
        services.AddHostedService<TrackingBackgroundService>();
        return services;
    }
}
