namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record FirmwareUpdateActionRequest(string PortName, string FirmwareFileName);
