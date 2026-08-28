using Microsoft.Extensions.Logging.Abstractions;
using TimeTracker.Core.Models;
using TimeTracker.Core.Services;

namespace TimeTracker.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _settingsPath;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), $"timetracker-settings-{Guid.NewGuid():N}.json");
        _store = new SettingsStore(_settingsPath, NullLogger<SettingsStore>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    [Fact]
    public void UpdateAppSetting_persists_snake_case_json()
    {
        Assert.True(_store.UpdateAppSetting("chrome.exe", "Chrome", "#FF0000", "Navegação"));

        var json = File.ReadAllText(_settingsPath);
        Assert.Contains("\"display_name\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hex_color\"", json, StringComparison.Ordinal);

        var settings = _store.GetAppSettings();
        Assert.Equal("Chrome", settings["chrome.exe"].DisplayName);
        Assert.Equal("#FF0000", settings["chrome.exe"].HexColor);
    }

    [Fact]
    public void UpdateChangedSettings_skips_unchanged_entries()
    {
        _store.UpdateAppSetting("code.exe", "VS Code", "#007ACC", "Desenvolvimento");

        var saved = _store.UpdateChangedSettings([
            new AppSettingUpdate
            {
                AppName = "code.exe",
                DisplayName = "VS Code",
                HexColor = "#007ACC",
                Category = "Desenvolvimento",
            },
            new AppSettingUpdate
            {
                AppName = "slack.exe",
                DisplayName = "Slack",
                HexColor = "#611F69",
                Category = "Comunicação",
            },
        ]);

        Assert.Equal(1, saved);
        Assert.Equal("Slack", _store.GetAppSettings()["slack.exe"].DisplayName);
    }
}
