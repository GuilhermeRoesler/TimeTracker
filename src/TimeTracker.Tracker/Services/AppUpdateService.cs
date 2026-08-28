using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimeTracker.Core;

namespace TimeTracker.Tracker.Services;

internal sealed record AppUpdateInfo(
    Version Version,
    string TagName,
    string SetupDownloadUrl,
    string ReleaseUrl);

internal sealed class AppUpdateService
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(ILogger<AppUpdateService> logger)
    {
        _logger = logger;
    }

    public static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? new Version(0, 0, 0)
            : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                AppConstants.GitHubReleasesLatestApi,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return null;
            }

            if (!AppVersion.TryParseTag(release.TagName, out var remoteVersion))
            {
                _logger.LogWarning("Tag de release inválida: {Tag}", release.TagName);
                return null;
            }

            var current = GetCurrentVersion();
            if (remoteVersion <= current)
            {
                return null;
            }

            var setupAsset = release.Assets?
                .FirstOrDefault(asset =>
                    asset.Name is not null &&
                    asset.Name.Contains("setup-win-x64", StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (setupAsset?.BrowserDownloadUrl is null)
            {
                _logger.LogWarning("Release {Tag} sem asset setup-win-x64.exe.", release.TagName);
                return null;
            }

            return new AppUpdateInfo(
                remoteVersion,
                release.TagName,
                setupAsset.BrowserDownloadUrl,
                release.HtmlUrl ?? $"https://github.com/{AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}/releases");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao verificar atualizações no GitHub.");
            return null;
        }
    }

    public async Task<string> DownloadSetupAsync(
        AppUpdateInfo update,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"TimeTrackerPro-{update.TagName.TrimStart('v')}-setup-win-x64.exe";
        var targetPath = Path.Combine(Path.GetTempPath(), fileName);

        using var response = await HttpClient.GetAsync(
            update.SetupDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var local = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;
            progress?.Report(totalRead);
        }

        return targetPath;
    }

    public static void LaunchSetupAndExit(string setupPath, Action exitApplication)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            UseShellExecute = true,
        });
        exitApplication();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TimeTrackerPro", GetCurrentVersion().ToString()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
