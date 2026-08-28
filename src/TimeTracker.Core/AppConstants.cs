namespace TimeTracker.Core;

public static class AppConstants
{
    public const string DbFileName = "productivity.db";
    public const string SettingsFileName = "app_settings.json";
    public const string AppDisplayName = "TimeTracker Pro";
    public const double DefaultPollIntervalSeconds = 5.0;
    public const int DashboardPort = 8501;
    public const string DashboardHost = "localhost";
    public static string DashboardUrl => $"http://{DashboardHost}:{DashboardPort}";

    public const string GitHubOwner = "GuilhermeRoesler";
    public const string GitHubRepo = "TimeTracker";
    public static string GitHubReleasesLatestApi =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
