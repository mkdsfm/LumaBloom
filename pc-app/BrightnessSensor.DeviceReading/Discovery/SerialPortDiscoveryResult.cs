namespace BrightnessSensor.DeviceReading.Discovery;

public sealed record SerialPortDiscoveryResult(
    SerialPortDiscoveryStatus Status,
    string? PortName,
    string? Error);
