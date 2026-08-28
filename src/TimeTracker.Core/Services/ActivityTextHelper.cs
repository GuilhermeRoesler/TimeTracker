namespace TimeTracker.Core.Services;

public static class ActivityTextHelper
{
    private static readonly string[] BrowserSuffixes =
    [
        " - Opera",
        " - Google Chrome",
        " - Microsoft Edge",
        " - Mozilla Firefox",
        " - Brave",
        " - Vivaldi",
        " - YouTube",
    ];

    public static string FormatDurationClean(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0)
        {
            return "0m";
        }

        var totalSeconds = (int)seconds;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
    }

    public static string FormatDurationDetailed(double seconds)
    {
        var totalSeconds = Math.Max(0, (int)seconds);
        var minutes = totalSeconds / 60;
        var secs = totalSeconds % 60;
        return $"{minutes}m {secs}s";
    }

    public static string CleanWindowTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Sem Título";
        }

        var clean = title;
        foreach (var suffix in BrowserSuffixes)
        {
            clean = clean.Replace(suffix, string.Empty, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(clean) ? "Sem Título" : clean;
    }
}
