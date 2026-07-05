using System.Diagnostics;
using System.IO;
using System.Net.Http;

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
    private static readonly string VersionUrl = $"https://raw.githubusercontent.com/{VersionInfo.RepositoryOwner}/{VersionInfo.RepositoryName}/master/latest-version.txt";

    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"OpenSpace/{VersionInfo.CurrentVersion}");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(VersionUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var tagName = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            var tagVersion = ParseVersion(tagName);
            if (tagVersion == null)
                return null;

            var currentVersion = new Version(VersionInfo.CurrentVersion);
            if (tagVersion <= currentVersion)
                return null;

            return new UpdateInfo(tagVersion, tagName);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return null;
        }
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
