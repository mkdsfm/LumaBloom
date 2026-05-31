using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal sealed record DeviceProfile(
    string ProfileId,
    string DeviceId,
    string SensorId,
    MeasurementKind MeasurementKind,
    ProcessingSettings Processing,
    CalibrationSettings Calibration,
    BrightnessSettings Brightness,
    int BaudRate = 115200,
    int DiscoveryTimeoutMs = 2500,
    bool IsGeneric = false);
