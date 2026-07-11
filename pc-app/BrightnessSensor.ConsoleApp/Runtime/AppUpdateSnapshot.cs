namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record AppUpdateSnapshot(
    string CurrentVersion,
    string LatestVersion,
    string StatusMessage,
    string? PackageName,
    bool UpdateAvailable,
    bool IsBusy,
    bool IncludePrerelease = false,
    bool IsPrerelease = false);
