using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSpace.Core;

internal sealed class UpdateInfo
{
    public Version Version { get; }
    public string TagName { get; }
    public string HtmlUrl { get; }
    public string? SetupDownloadUrl { get; }
    public string? PortableDownloadUrl { get; }
    public string? SetupFileName { get; }
    public string? PortableFileName { get; }

    public UpdateInfo(Version version, string tagName, string htmlUrl, IReadOnlyList<GitHubAsset> assets)
    {
        Version = version;
        TagName = tagName;
        HtmlUrl = htmlUrl;

        var setupAsset = assets.FirstOrDefault(a => a.Name?.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) == true);
        var portableAsset = assets.FirstOrDefault(a => a.Name?.EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase) == true);

        SetupDownloadUrl = setupAsset?.BrowserDownloadUrl;
        SetupFileName = setupAsset?.Name;
        PortableDownloadUrl = portableAsset?.BrowserDownloadUrl;
        PortableFileName = portableAsset?.Name;
    }
}

internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"OpenSpace/{VersionInfo.CurrentVersion}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(VersionInfo.GitHubReleasesApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release?.TagName == null)
                return null;

            var tagVersion = ParseVersion(release.TagName);
            if (tagVersion == null)
                return null;

            var currentVersion = new Version(VersionInfo.CurrentVersion);
            if (tagVersion <= currentVersion)
                return null;

            return new UpdateInfo(tagVersion, release.TagName, release.HtmlUrl ?? VersionInfo.GitHubReleasesPageUrl, release.Assets ?? new List<GitHubAsset>());
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return null;
        }
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(update.SetupDownloadUrl) || string.IsNullOrEmpty(update.SetupFileName))
            return null;

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

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}
