using TimeTracker.Core;

namespace TimeTracker.Tracker;

/// <summary>
/// Mantém a User Data Folder do WebView2 alinhada à versão do app.
/// Após upgrade, o cache HTTP do Chromium pode servir JS/CSS antigos;
/// apagar a pasta na mudança de versão evita UI stale.
/// </summary>
internal static class WebView2ProfileCache
{
    public const string FolderName = "WebView2";
    private const string VersionMarkerFileName = "webview2-profile-version.txt";

    public static string GetUserDataFolder() =>
        Path.Combine(AppPaths.GetDataDir(), FolderName);

    public static string GetVersionMarkerPath() =>
        Path.Combine(AppPaths.GetDataDir(), VersionMarkerFileName);

    public static void InvalidateIfVersionChanged(Version currentVersion)
    {
        var versionText = FormatVersion(currentVersion);
        var markerPath = GetVersionMarkerPath();

        string? previous = null;
        try
        {
            if (File.Exists(markerPath))
            {
                previous = File.ReadAllText(markerPath).Trim();
            }
        }
        catch (IOException)
        {
            // Segue com invalidação.
        }
        catch (UnauthorizedAccessException)
        {
            // Segue com invalidação.
        }

        if (string.Equals(previous, versionText, StringComparison.Ordinal))
        {
            return;
        }

        TryDeleteUserDataFolder();
        TryWriteVersionMarker(markerPath, versionText);
    }

    public static void TryDeleteUserDataFolder()
    {
        var folder = GetUserDataFolder();
        if (!Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // Pasta em uso — best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permissão — best effort.
        }
    }

    internal static string FormatVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    private static void TryWriteVersionMarker(string markerPath, string versionText)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.GetDataDir());
            File.WriteAllText(markerPath, versionText);
        }
        catch (IOException)
        {
            // Ignorar; tentará de novo no próximo arranque.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorar.
        }
    }
}
