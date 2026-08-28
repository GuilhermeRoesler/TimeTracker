using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TimeTracker.Core;

namespace TimeTracker.Tracker.Services;

internal sealed class StartupShortcutService
{
    private readonly ILogger<StartupShortcutService> _logger;

    public StartupShortcutService(ILogger<StartupShortcutService> logger)
    {
        _logger = logger;
    }

    public void EnsureStartupShortcut(string appDir, string executablePath)
    {
        try
        {
            var startupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup");
            Directory.CreateDirectory(startupFolder);

            var shortcutPath = Path.Combine(startupFolder, $"{AppConstants.AppDisplayName}.lnk");
            var target = executablePath;
            var arguments = string.Empty;

            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell não disponível.");

            dynamic shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Falha ao criar WScript.Shell.");

            var needsWrite = true;
            if (File.Exists(shortcutPath))
            {
                dynamic existing = shell.CreateShortcut(shortcutPath);
                var expectedIcon = $"{target},0";
                needsWrite = !string.Equals(
                    NormalizePath(existing.Targetpath),
                    NormalizePath(target),
                    StringComparison.OrdinalIgnoreCase)
                    || !string.Equals((string)existing.Arguments, arguments, StringComparison.Ordinal)
                    || !string.Equals(
                        NormalizePath(existing.WorkingDirectory),
                        NormalizePath(appDir),
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        (string)existing.IconLocation,
                        expectedIcon,
                        StringComparison.OrdinalIgnoreCase);
            }

            if (needsWrite)
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.Targetpath = target;
                shortcut.Arguments = arguments;
                shortcut.WorkingDirectory = appDir;
                shortcut.WindowStyle = 7;
                shortcut.Description = AppConstants.AppDisplayName;
                shortcut.IconLocation = $"{target},0";
                shortcut.Save();
            }

            foreach (var legacyName in new[] { $"{AppConstants.AppDisplayName}.vbs", $"{AppConstants.AppDisplayName}.bat" })
            {
                var legacyPath = Path.Combine(startupFolder, legacyName);
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar atalho de inicialização.");
        }
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
}
