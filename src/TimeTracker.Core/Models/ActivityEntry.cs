namespace TimeTracker.Core.Models;

public sealed class ActivityEntry
{
    public long Id { get; init; }

    public string AppName { get; init; } = string.Empty;

    public string WindowTitle { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime? EndTime { get; init; }

    public double DurationSeconds { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? HexColor { get; init; }

    public string Category { get; init; } = "Sem Categoria";

    public DateOnly Date { get; init; }

    public int Hour { get; init; }
}
