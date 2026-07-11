namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record GitHubReleaseInfo(
    string Tag,
    string Version,
    Uri PackageUrl,
    string PackageName,
    string ReleasePageUrl,
    bool IsPrerelease);
