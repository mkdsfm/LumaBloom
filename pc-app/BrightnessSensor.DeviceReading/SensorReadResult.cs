namespace BrightnessSensor.DeviceReading;

public readonly record struct SensorReadResult(
    SensorReadStatus Status,
    SensorMessage? Message,
    string? RawLine,
    string? Error);
