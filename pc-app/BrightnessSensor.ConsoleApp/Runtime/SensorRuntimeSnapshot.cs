namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record SensorRuntimeSnapshot(
    string DeviceId,
    string SensorId,
    long DeviceTimestamp,
    int Value,
    int? Raw,
    bool Calibrated,
    DateTimeOffset ReceivedAt);
