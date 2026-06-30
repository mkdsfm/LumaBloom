namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record MonitorRuntimeSnapshot(
    string Source,
    string Name,
    bool IsEnabled,
    int? LastAppliedBrightness,
    int? LastRequestedBrightness,
    double? LastNormalized,
    double? LastFiltered,
    DateTimeOffset? LastUpdatedAt,
    string? LastError,
    string LastStatus);
