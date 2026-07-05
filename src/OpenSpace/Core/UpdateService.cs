using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenSpace.Core;

internal sealed class UpdateInfo
{
    public Version Version { get; }
    public string TagName { get; }
    public string SetupDownloadUrl { get; }
    public string SetupFileName { get; }

    public UpdateInfo(Version version, string tagName)
    {
        Version = version;
        TagName = tagName;

        var versionString = tagName.TrimStart('v', 'V');
        SetupFileName = $"OpenSpace-{versionString}-setup.exe";
        SetupDownloadUrl = $"https://github.com/{VersionInfo.RepositoryOwner}/{VersionInfo.RepositoryName}/releases/download/{tagName}/{SetupFileName}";
    }
}

internal sealed class UpdateService : IDisposable
{
    private static readonly string RawVersionUrl = $"https://raw.githubusercontent.com/{VersionInfo.RepositoryOwner}/{VersionInfo.RepositoryName}/master/latest-version.txt";
    private static readonly string GitHubApiUrl = $"https://api.github.com/repos/{VersionInfo.RepositoryOwner}/{VersionInfo.RepositoryName}/releases/latest";
    private static readonly string GitHubHtmlUrl = $"https://github.com/{VersionInfo.RepositoryOwner}/{VersionInfo.RepositoryName}/releases/latest";

    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"OpenSpace/{VersionInfo.CurrentVersion}");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string? latestTag = null;
        Exception? lastException = null;

        // Source 1: raw.githubusercontent.com (highest rate limits)
        try
        {
            latestTag = await FetchRawVersionAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestTag))
                return BuildUpdateInfo(latestTag);
        }
        catch (Exception ex)
        {
            lastException = ex;
            App.LogException(new Exception("Raw version check failed", ex));
        }

        // Source 2: GitHub API
        try
        {
            latestTag = await FetchApiVersionAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestTag))
                return BuildUpdateInfo(latestTag);
        }
        catch (Exception ex)
        {
            lastException = ex;
            App.LogException(new Exception("GitHub API version check failed", ex));
        }

        // Source 3: GitHub releases HTML page
        try
        {
            latestTag = await FetchHtmlVersionAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestTag))
                return BuildUpdateInfo(latestTag);
        }
        catch (Exception ex)
        {
            lastException = ex;
            App.LogException(new Exception("GitHub HTML version check failed", ex));
        }

        if (lastException != null)
        {
            App.LogException(new Exception("All update sources failed", lastException));
        }

        return null;
    }

    private static UpdateInfo? BuildUpdateInfo(string tagName)
    {
        var tagVersion = ParseVersion(tagName);
        if (tagVersion == null)
            return null;

        var currentVersion = new Version(VersionInfo.CurrentVersion);
        if (tagVersion <= currentVersion)
            return null;

        return new UpdateInfo(tagVersion, tagName);
    }

    private async Task<string?> FetchRawVersionAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RawVersionUrl);
        request.Headers.TryAddWithoutValidation("Accept", "text/plain");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
    }

    private async Task<string?> FetchApiVersionAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);
        return release?.TagName;
    }

    private async Task<string?> FetchHtmlVersionAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubHtmlUrl);
        request.Headers.TryAddWithoutValidation("Accept", "text/html");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        // Try to find the latest release tag in the HTML title or URL.
        var match = Regex.Match(html, @"/releases/tag/([^""\s]+)");
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return null;
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), update.SetupFileName);

        try
        {
            using var response = await _httpClient.GetAsync(update.SetupDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long readBytes = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readBytes += read;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)readBytes / totalBytes * 100);
                }
            }

            return tempPath;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return null;
        }
    }

    public void RunInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    private static Version? ParseVersion(string tag)
    {
        var versionString = tag.TrimStart('v', 'V');
        if (Version.TryParse(versionString, out var version))
            return version;
        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
}
