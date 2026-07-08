using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal sealed record ResolvedAppSettings(
    string ProtocolId,
    MeasurementKind MeasurementKind,
    int BaudRate,
    int DiscoveryTimeoutMs,
    ProcessingSettings Processing,
    BrightnessSettings Brightness);
