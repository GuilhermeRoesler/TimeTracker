namespace TimeTracker.Core;

public static class AppPaths
{
    public const string DataDirEnvironmentVariable = "TIMETRACKER_DATA_DIR";

    private static string? _dataDir;
    private static string? _installDir;

    /// <summary>
    /// Pasta com dados do usuário (DB + settings). Em produção: %LocalAppData%\TimeTracker Pro.
    /// </summary>
    public static string GetDataDir()
    {
        EnsureInitialized();
        return _dataDir!;
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

    public static void Configure(string dataDir, string installDir)
    {
        _dataDir = Path.GetFullPath(dataDir);
        _installDir = Path.GetFullPath(installDir);
        Directory.CreateDirectory(_dataDir);
    }

    /// <summary>Define data e install no mesmo diretório (dev / testes).</summary>
    public static void SetAppDir(string appDir) => Configure(appDir, appDir);

    public static void InitializeFromEnvironmentOrDefaults(string? contentRoot = null)
    {
        if (_dataDir is not null && _installDir is not null)
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
            Configure(solutionRoot, solutionRoot);
            return;
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDisplayName);
        Configure(dataDir, installRoot);
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

    private static void EnsureInitialized()
    {
        if (_dataDir is null || _installDir is null)
        {
            InitializeFromEnvironmentOrDefaults();
        }
    }
}
