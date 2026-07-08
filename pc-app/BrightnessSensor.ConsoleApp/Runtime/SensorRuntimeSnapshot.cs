namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record SensorRuntimeSnapshot(
    string Id,
    long DeviceTimestamp,
    int? Raw,
    DateTimeOffset ReceivedAt);
