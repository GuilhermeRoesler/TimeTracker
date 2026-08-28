using Microsoft.Extensions.Logging;
using TimeTracker.Core.Abstractions;

namespace TimeTracker.Core.Services;

public sealed class TrackingEngine
{
    private readonly IActiveWindowProvider _windowProvider;
    private readonly ActivityRepository _repository;
    private readonly ILogger<TrackingEngine> _logger;
    private readonly double _pollIntervalSeconds;

    public TrackingEngine(
        IActiveWindowProvider windowProvider,
        ActivityRepository repository,
        ILogger<TrackingEngine> logger,
        double pollIntervalSeconds = AppConstants.DefaultPollIntervalSeconds)
    {
        _windowProvider = windowProvider;
        _repository = repository;
        _logger = logger;
        _pollIntervalSeconds = pollIntervalSeconds;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando monitoramento...");

        var sessionStartUnix = GetUnixTimestamp();
        string? lastApp = null;
        string? lastTitle = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var activeWindow = _windowProvider.GetActiveWindow();
                    var currentApp = activeWindow?.AppName;
                    var currentTitle = activeWindow?.WindowTitle;

                    if (!string.Equals(currentApp, lastApp, StringComparison.Ordinal) ||
                        !string.Equals(currentTitle, lastTitle, StringComparison.Ordinal))
                    {
                        var endTime = GetUnixTimestamp();
                        if (lastApp is not null)
                        {
                            _repository.SaveActivity(lastApp, lastTitle, sessionStartUnix, endTime);
                        }

                        sessionStartUnix = endTime;
                        lastApp = currentApp;
                        lastTitle = currentTitle;
                    }

                    await WaitForNextPollAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Erro no tracker.");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            if (lastApp is not null)
            {
                _repository.SaveActivity(lastApp, lastTitle, sessionStartUnix, GetUnixTimestamp());
            }
        }
        catch (OperationCanceledException)
        {
            if (lastApp is not null)
            {
                _repository.SaveActivity(lastApp, lastTitle, sessionStartUnix, GetUnixTimestamp());
            }
        }
    }

    private async Task WaitForNextPollAsync(CancellationToken cancellationToken)
    {
        var elapsed = 0.0;
        while (elapsed < _pollIntervalSeconds && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            elapsed += 0.1;
        }
    }

    private static double GetUnixTimestamp()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
}
