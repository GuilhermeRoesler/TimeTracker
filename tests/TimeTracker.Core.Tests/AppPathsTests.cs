using TimeTracker.Core;

namespace TimeTracker.Core.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void MigrateLegacyProductionDataIfNeeded_moves_only_db_and_settings_into_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "tt-migrate-" + Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(root, "TimeTracker Pro");
        var dataDir = Path.Combine(appRoot, "data");
        Directory.CreateDirectory(appRoot);

        File.WriteAllText(Path.Combine(appRoot, AppConstants.DbFileName), "db");
        File.WriteAllText(Path.Combine(appRoot, AppConstants.DbFileName + "-wal"), "wal");
        File.WriteAllText(Path.Combine(appRoot, AppConstants.SettingsFileName), "{}");
        Directory.CreateDirectory(Path.Combine(appRoot, AppPaths.WebView2FolderName));
        File.WriteAllText(Path.Combine(appRoot, AppPaths.WebView2FolderName, "marker"), "x");
        File.WriteAllText(Path.Combine(appRoot, AppPaths.WebView2VersionMarkerFileName), "1.0.0");

        try
        {
            AppPaths.MigrateLegacyProductionDataIfNeeded(appRoot, dataDir);

            Assert.True(File.Exists(Path.Combine(dataDir, AppConstants.DbFileName)));
            Assert.True(File.Exists(Path.Combine(dataDir, AppConstants.DbFileName + "-wal")));
            Assert.True(File.Exists(Path.Combine(dataDir, AppConstants.SettingsFileName)));
            Assert.False(File.Exists(Path.Combine(appRoot, AppConstants.DbFileName)));

            // WebView2 permanece irmão de data/
            Assert.True(Directory.Exists(Path.Combine(appRoot, AppPaths.WebView2FolderName)));
            Assert.False(Directory.Exists(Path.Combine(dataDir, AppPaths.WebView2FolderName)));
            Assert.True(File.Exists(Path.Combine(appRoot, AppPaths.WebView2VersionMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(dataDir, AppPaths.WebView2VersionMarkerFileName)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void MigrateLegacyProductionDataIfNeeded_hoists_webview2_out_of_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "tt-migrate-" + Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(root, "TimeTracker Pro");
        var dataDir = Path.Combine(appRoot, "data");
        Directory.CreateDirectory(dataDir);

        File.WriteAllText(Path.Combine(dataDir, AppConstants.DbFileName), "db");
        Directory.CreateDirectory(Path.Combine(dataDir, AppPaths.WebView2FolderName));
        File.WriteAllText(Path.Combine(dataDir, AppPaths.WebView2FolderName, "marker"), "x");
        File.WriteAllText(Path.Combine(dataDir, AppPaths.WebView2VersionMarkerFileName), "1.0.0");

        try
        {
            AppPaths.MigrateLegacyProductionDataIfNeeded(appRoot, dataDir);

            Assert.True(Directory.Exists(Path.Combine(appRoot, AppPaths.WebView2FolderName)));
            Assert.False(Directory.Exists(Path.Combine(dataDir, AppPaths.WebView2FolderName)));
            Assert.True(File.Exists(Path.Combine(appRoot, AppPaths.WebView2VersionMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(dataDir, AppPaths.WebView2VersionMarkerFileName)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void MigrateLegacyProductionDataIfNeeded_skips_when_destination_db_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "tt-migrate-" + Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(root, "TimeTracker Pro");
        var dataDir = Path.Combine(appRoot, "data");
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(appRoot);

        File.WriteAllText(Path.Combine(dataDir, AppConstants.DbFileName), "new");
        File.WriteAllText(Path.Combine(appRoot, AppConstants.DbFileName), "legacy");

        try
        {
            AppPaths.MigrateLegacyProductionDataIfNeeded(appRoot, dataDir);

            Assert.Equal("new", File.ReadAllText(Path.Combine(dataDir, AppConstants.DbFileName)));
            Assert.True(File.Exists(Path.Combine(appRoot, AppConstants.DbFileName)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void InferUserRoot_uses_parent_when_data_leaf()
    {
        var dataDir = @"C:\Users\x\AppData\Local\TimeTracker Pro\data";
        var root = AppPaths.InferUserRoot(dataDir);
        Assert.Equal(@"C:\Users\x\AppData\Local\TimeTracker Pro", root);
    }
}