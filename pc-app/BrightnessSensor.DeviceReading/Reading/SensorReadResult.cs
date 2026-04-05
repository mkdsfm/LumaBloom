using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.DeviceReading.Reading;

public readonly record struct SensorReadResult(
    SensorReadStatus Status,
    SensorMessage? Message,
    string? RawLine,
    string? Error);
