namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record SerialPortInfo(string PortName, string? Description, bool IsEspressifDevice);
