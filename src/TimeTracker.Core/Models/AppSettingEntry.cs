namespace TimeTracker.Core.Models;

public sealed class AppSettingEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string? HexColor { get; set; }

    public string? Category { get; set; }
}
