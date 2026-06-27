namespace BrightnessSensor.DeviceReading.Reading;

public readonly record struct SerialLineReadResult(
    SensorReadStatus Status,
    string? Line,
    string? Error);
