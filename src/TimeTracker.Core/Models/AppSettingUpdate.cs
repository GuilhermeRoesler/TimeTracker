namespace TimeTracker.Core.Models;

public sealed class AppSettingUpdate
{
    public string AppName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? HexColor { get; init; }

    public string? Category { get; init; }
}
