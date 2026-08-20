using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class SerialPortCatalog(
    Func<string[]>? getPortNames = null,
    Func<IReadOnlyDictionary<string, SerialPortInfo>>? getPortInfo = null)
{
    private static readonly Regex PortNamePattern = new(@"\((COM\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Func<string[]> _getPortNames = getPortNames ?? SerialPort.GetPortNames;
    private readonly Func<IReadOnlyDictionary<string, SerialPortInfo>> _getPortInfo = getPortInfo ?? GetWindowsPortInfo;

    public SerialPortCatalogResult GetPorts()
    {
        try
        {
            var portNames = _getPortNames()
                .Where(portName => !string.IsNullOrWhiteSpace(portName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(portName => portName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var portInfo = TryGetPortInfo();
            var ports = portNames
                .Select(portName => portInfo.TryGetValue(portName, out var info)
                    ? info with { PortName = portName }
                    : new SerialPortInfo(portName, null, IsEspressifDevice: false))
                .ToArray();
            return new SerialPortCatalogResult(ports, null);
        }
        catch (Exception exception)
        {
            return new SerialPortCatalogResult([], $"Failed to enumerate COM ports: {exception.Message}");
        }
    }

    private IReadOnlyDictionary<string, SerialPortInfo> TryGetPortInfo()
    {
        try
        {
            return _getPortInfo();
        }
        catch
        {
            return new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, SerialPortInfo> GetWindowsPortInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher("SELECT Name, Description, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
        using var devices = searcher.Get();
        foreach (var o in devices)
        {
            var device = (ManagementObject)o;
            var name = Convert.ToString(device["Name"]);
            var match = PortNamePattern.Match(name ?? string.Empty);
            if (!match.Success)
            {
                continue;
            }

            var portName = match.Groups[1].Value;
            var description = PortNamePattern.Replace(name!, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                description = Convert.ToString(device["Description"]);
            }

            var pnpDeviceId = Convert.ToString(device["PNPDeviceID"]);
            var isEspressifDevice = pnpDeviceId?.Contains("VID_303A", StringComparison.OrdinalIgnoreCase) == true;
            result[portName] = new SerialPortInfo(portName, description, isEspressifDevice);
        }

        return result;
    }
}
