using System.Reflection;

namespace OpenSpace.Core;

internal static class VersionInfo
{
    public static string CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static readonly string RepositoryOwner = "Nachosia";
    public static readonly string RepositoryName = "OpenSpace";
    public static readonly string GitHubReleasesApiUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
    public static readonly string GitHubReleasesPageUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest";
}
