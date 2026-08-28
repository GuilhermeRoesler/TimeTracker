using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTracker.Core.Models;

namespace TimeTracker.Core.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _settingsPath;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(string settingsPath, ILogger<SettingsStore> logger)
    {
        _settingsPath = settingsPath;
        _logger = logger;
    }

    public static SettingsStore FromAppPaths(ILogger<SettingsStore> logger)
        => new(AppPaths.GetSettingsPath(), logger);

    public IReadOnlyDictionary<string, AppSettingEntry> GetAppSettings()
        => LoadFile().Apps;

    public bool UpdateAppSetting(
        string appName,
        string displayName,
        string? hexColor = null,
        string? category = null)
    {
        var config = LoadFile();
        config.Apps[appName] = new AppSettingEntry
        {
            DisplayName = displayName,
            HexColor = hexColor,
            Category = category,
        };
        return SaveFile(config);
    }

    private SettingsFile LoadFile()
    {
        if (!File.Exists(_settingsPath))
        {
            return new SettingsFile();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions);
            if (data?.Apps is not null)
            {
                return data;
            }

            _logger.LogError("{File} inválido: esperado objeto com chave 'apps'.", AppConstants.SettingsFileName);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogError(ex, "Erro ao ler {File}.", AppConstants.SettingsFileName);
        }

        return new SettingsFile();
    }

    private bool SaveFile(SettingsFile config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_settingsPath, json + Environment.NewLine);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Erro ao salvar {File}.", AppConstants.SettingsFileName);
            return false;
        }
    }

    private sealed class SettingsFile
    {
        public Dictionary<string, AppSettingEntry> Apps { get; set; } = new();
    }
}
