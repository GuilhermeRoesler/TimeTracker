using Microsoft.Extensions.Logging.Abstractions;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Models;
using TimeTracker.Core.Services;

namespace TimeTracker.Core.Tests;

public class TrackingEngineTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ActivityRepository _repository;

    public TrackingEngineTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"timetracker-engine-{Guid.NewGuid():N}.db");
        _repository = new ActivityRepository(_dbPath, NullLogger<ActivityRepository>.Instance);
    }

    public void Dispose() => TestDatabaseHelper.DeleteDatabase(_dbPath);

    [Fact]
    public async Task RunAsync_saves_activity_on_window_change()
    {
        var provider = new QueueWindowProvider([
            new ActiveWindowInfo("a.exe", "Window A"),
            new ActiveWindowInfo("b.exe", "Window B"),
        ]);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var engine = new TrackingEngine(
            provider,
            _repository,
            NullLogger<TrackingEngine>.Instance,
            pollIntervalSeconds: 1.1);

        await engine.RunAsync(cts.Token);

        var apps = _repository.GetAllApps();
        Assert.Contains("a.exe", apps);
        Assert.Contains("b.exe", apps);
    }

    [Fact]
    public async Task RunAsync_flushes_active_session_on_cancel()
    {
        var provider = new QueueWindowProvider([
            new ActiveWindowInfo("code.exe", "main.cs"),
        ]);

        using var cts = new CancellationTokenSource(1500);
        var engine = new TrackingEngine(
            provider,
            _repository,
            NullLogger<TrackingEngine>.Instance,
            pollIntervalSeconds: 0.2);

        await engine.RunAsync(cts.Token);

        var activities = _repository.GetAllActivities();
        Assert.Single(activities);
        Assert.Equal("code.exe", activities[0].AppName);
        Assert.True(activities[0].DurationSeconds >= 1.0);
    }

    private sealed class QueueWindowProvider(IReadOnlyList<ActiveWindowInfo?> windows) : IActiveWindowProvider
    {
        private int _index;

        public ActiveWindowInfo? GetActiveWindow()
            => _index < windows.Count ? windows[_index++] : windows[^1];
    }
}
