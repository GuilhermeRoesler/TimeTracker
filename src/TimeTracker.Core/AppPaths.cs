namespace TimeTracker.Core;

public static class AppPaths
{
    public const string DataDirEnvironmentVariable = "TIMETRACKER_DATA_DIR";
    public const string WebView2FolderName = "WebView2";
    public const string WebView2VersionMarkerFileName = "webview2-profile-version.txt";

    private static string? _dataDir;
    private static string? _installDir;
    private static string? _userRoot;

    /// <summary>
    /// Pasta com DB + settings.
    /// Em produção: %LocalAppData%\TimeTracker Pro\data.
    /// </summary>
    public static string GetDataDir()
    {
        EnsureInitialized();
        return _dataDir!;
    }

    /// <summary>
    /// Raiz do produto no perfil do usuário (pai de <c>data/</c> e <c>WebView2/</c>).
    /// Em produção: %LocalAppData%\TimeTracker Pro. Em dev: raiz do repo.
    /// </summary>
    public static string GetUserRoot()
    {
        EnsureInitialized();
        return _userRoot!;
    }

    /// <summary>
    /// Pasta dos executáveis (instalação). Em dev: raiz do repo.
    /// </summary>
    public static string GetInstallDir()
    {
        EnsureInitialized();
        return _installDir!;
    }

    /// <summary>
    /// True quando o processo roda a partir da árvore do repositório (ex.: <c>run.bat</c> / <c>dotnet run</c>).
    /// Instalação publicada (Program Files / portable) retorna false.
    /// </summary>
    public static bool IsDevelopmentRun()
    {
        EnsureInitialized();
        return FindSolutionRoot(_installDir!) is not null;
    }

    /// <summary>Compatível com chamadas existentes — aponta para a pasta de dados.</summary>
    public static string GetAppDir() => GetDataDir();

    public static string GetDbPath() => Path.Combine(GetDataDir(), AppConstants.DbFileName);

    public static string GetSettingsPath() => Path.Combine(GetDataDir(), AppConstants.SettingsFileName);

    public static string GetWebView2UserDataFolder() =>
        Path.Combine(GetUserRoot(), WebView2FolderName);

    public static string GetWebView2VersionMarkerPath() =>
        Path.Combine(GetUserRoot(), WebView2VersionMarkerFileName);

    /// <summary>
    /// Raiz do produto em LocalAppData (pai de <c>data/</c> e <c>WebView2/</c>).
    /// </summary>
    public static string GetDefaultProductionAppRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDisplayName);

    public static string GetDefaultProductionDataDir() =>
        Path.Combine(GetDefaultProductionAppRoot(), AppConstants.UserDataFolderName);

    public static void Configure(string dataDir, string installDir, string? userRoot = null)
    {
        _dataDir = Path.GetFullPath(dataDir);
        _installDir = Path.GetFullPath(installDir);
        _userRoot = Path.GetFullPath(userRoot ?? InferUserRoot(_dataDir));
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_userRoot);
    }

    /// <summary>Define data e install no mesmo diretório (dev / testes).</summary>
    public static void SetAppDir(string appDir) => Configure(appDir, appDir, appDir);

    public static void InitializeFromEnvironmentOrDefaults(string? contentRoot = null)
    {
        if (_dataDir is not null && _installDir is not null && _userRoot is not null)
        {
            return;
        }

        var fromEnv = Environment.GetEnvironmentVariable(DataDirEnvironmentVariable);
        var installRoot = Path.GetFullPath(
            (contentRoot ?? AppContext.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            Configure(fromEnv, installRoot);
            return;
        }

        var solutionRoot = FindSolutionRoot(installRoot);
        if (solutionRoot is not null)
        {
            Configure(solutionRoot, solutionRoot, solutionRoot);
            return;
        }

        var appRoot = GetDefaultProductionAppRoot();
        var dataDir = GetDefaultProductionDataDir();
        MigrateLegacyProductionDataIfNeeded(appRoot, dataDir);
        Configure(dataDir, installRoot, appRoot);
    }

    /// <summary>
    /// Move DB/settings da raiz legada para <c>data/</c>.
    /// WebView2 permanece irmão de <c>data/</c> na raiz do produto;
    /// se estiver dentro de <c>data/</c> (layout incorreto), sobe para a raiz.
    /// </summary>
    public static void MigrateLegacyProductionDataIfNeeded(string appRoot, string dataDir)
    {
        appRoot = Path.GetFullPath(appRoot);
        dataDir = Path.GetFullPath(dataDir);

        if (string.Equals(appRoot, dataDir, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(appRoot))
        {
            return;
        }

        Directory.CreateDirectory(dataDir);

        if (!File.Exists(Path.Combine(dataDir, AppConstants.DbFileName)))
        {
            MoveIfExists(Path.Combine(appRoot, AppConstants.DbFileName), Path.Combine(dataDir, AppConstants.DbFileName));
            MoveIfExists(Path.Combine(appRoot, AppConstants.DbFileName + "-wal"), Path.Combine(dataDir, AppConstants.DbFileName + "-wal"));
            MoveIfExists(Path.Combine(appRoot, AppConstants.DbFileName + "-shm"), Path.Combine(dataDir, AppConstants.DbFileName + "-shm"));
            MoveIfExists(Path.Combine(appRoot, AppConstants.SettingsFileName), Path.Combine(dataDir, AppConstants.SettingsFileName));
        }

        // Marker e WebView2 ficam na raiz do produto (irmãos de data/).
        MoveIfExists(
            Path.Combine(dataDir, WebView2VersionMarkerFileName),
            Path.Combine(appRoot, WebView2VersionMarkerFileName));

        var misplacedWebView2 = Path.Combine(dataDir, WebView2FolderName);
        var targetWebView2 = Path.Combine(appRoot, WebView2FolderName);
        if (Directory.Exists(misplacedWebView2) && !Directory.Exists(targetWebView2))
        {
            try
            {
                Directory.Move(misplacedWebView2, targetWebView2);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public static string? FindSolutionRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TimeTracker.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Se <paramref name="dataDir"/> termina em <c>data</c>, o pai é a raiz do produto;
    /// caso contrário (dev), data e WebView2 compartilham o mesmo diretório.
    /// </summary>
    public static string InferUserRoot(string dataDir)
    {
        var trimmed = dataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = Path.GetFileName(trimmed);
        if (leaf.Equals(AppConstants.UserDataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetDirectoryName(trimmed);
            if (!string.IsNullOrEmpty(parent))
            {
                return parent;
            }
        }

        return trimmed;
    }

    private static void MoveIfExists(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination))
        {
            return;
        }

        try
        {
            File.Move(source, destination);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void EnsureInitialized()
    {
        if (_dataDir is null || _installDir is null || _userRoot is null)
        {
            InitializeFromEnvironmentOrDefaults();
        }
    }
}
