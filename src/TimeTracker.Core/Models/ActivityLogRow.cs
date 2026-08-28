namespace TimeTracker.Core.Models;

public sealed class ActivityLogRow
{
    public long Id { get; init; }

    public string AppName { get; init; } = string.Empty;

    public string WindowTitle { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime? EndTime { get; init; }

    public double DurationSeconds { get; init; }
}
