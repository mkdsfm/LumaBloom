using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal sealed record ResolvedAppSettings(
    string ProfileId,
    MeasurementKind MeasurementKind,
    bool IsGenericProfile,
    string? DiscoveryDeviceId,
    int BaudRate,
    int DiscoveryTimeoutMs,
    ProcessingSettings Processing,
    BrightnessSettings Brightness,
    CalibrationSettings Calibration);
