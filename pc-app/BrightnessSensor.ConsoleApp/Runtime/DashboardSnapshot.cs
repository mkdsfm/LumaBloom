namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record DashboardSnapshot(
    AppLifecycleState LifecycleState,
    string StatusMessage,
    string CalibrationStatus,
    bool IsPaused,
    bool ShowEventLog,
    bool RecalibrationPending,
    int? PendingCalibrationBrightnessPercent,
    bool IsCalibrationInputActive,
    string CalibrationInputBuffer,
    string? PortName,
    int? BaudRate,
    string? ConnectionSummary,
    string? ProfileId,
    string? ProfileSummary,
    string? MeasurementKind,
    bool? IsGenericProfile,
    SensorRuntimeSnapshot? LatestSensor,
    IReadOnlyList<MonitorRuntimeSnapshot> Monitors,
    IReadOnlyList<RuntimeEventEntry> Events);
