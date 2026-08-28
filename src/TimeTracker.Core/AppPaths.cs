namespace TimeTracker.Core;

public static class AppPaths
{
    private static string? _appDir;

    public static string GetAppDir()
    {
        if (_appDir is not null)
        {
            return _appDir;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _appDir = Path.GetFullPath(baseDir);
        return _appDir;
    }

    public static void SetAppDir(string appDir)
    {
        _appDir = Path.GetFullPath(appDir);
    }

    public static string GetDbPath() => Path.Combine(GetAppDir(), AppConstants.DbFileName);

    public static string GetSettingsPath() => Path.Combine(GetAppDir(), AppConstants.SettingsFileName);
}
