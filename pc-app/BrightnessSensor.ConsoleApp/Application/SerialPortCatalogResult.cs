namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record SerialPortCatalogResult(IReadOnlyList<SerialPortInfo> Ports, string? Error)
{
    public bool IsSuccess => Error is null;

    public IReadOnlyList<string> PortNames => Ports.Select(port => port.PortName).ToArray();
}
