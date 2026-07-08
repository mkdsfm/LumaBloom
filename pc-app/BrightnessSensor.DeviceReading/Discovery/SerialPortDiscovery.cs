using System.IO.Ports;
using BrightnessSensor.DeviceReading.Models;
namespace BrightnessSensor.DeviceReading.Discovery;

public sealed class SerialPortDiscovery
{
    private readonly int _baudRate;
    private readonly int _discoveryTimeoutMs;
    private readonly string _newLine;
    private readonly int _readTimeoutMs;

    public SerialPortDiscovery(
        int baudRate = 115200,
        int discoveryTimeoutMs = 2500,
        int readTimeoutMs = 250,
        string newLine = "\n")
    {
        ValidateArguments(baudRate, discoveryTimeoutMs, readTimeoutMs, newLine);
        _baudRate = baudRate;
        _discoveryTimeoutMs = discoveryTimeoutMs;
        _readTimeoutMs = readTimeoutMs;
        _newLine = newLine;
    }

    public SerialPortDiscoveryResult ResolveFirstTelemetry()
    {
        var orderedPortNames = SerialPort.GetPortNames()
            .OrderBy(portName => portName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderedPortNames.Length == 0)
        {
            return new SerialPortDiscoveryResult(
                SerialPortDiscoveryStatus.NoPortsAvailable,
                null,
                "No COM ports are available for discovery.");
        }

        var checkedPorts = new List<string>(orderedPortNames.Length);
        var diagnostics = new List<string>();
        var discoveryTimeout = TimeSpan.FromMilliseconds(_discoveryTimeoutMs);

        foreach (var portName in orderedPortNames)
        {
            checkedPorts.Add(portName);

            var probeResult = TryProbePort(
                portName,
                discoveryTimeout,
                message => LumaBloomTelemetry.HasSupportedProtocolId(message.Id) && message.Raw.HasValue);

            if (probeResult.IsMatch)
            {
                return new SerialPortDiscoveryResult(
                    SerialPortDiscoveryStatus.Success,
                    portName,
                    null);
            }

            if (!string.IsNullOrWhiteSpace(probeResult.Diagnostic))
            {
                diagnostics.Add(probeResult.Diagnostic);
            }
        }

        var diagnosticsSuffix = diagnostics.Count == 0
            ? string.Empty
            : $" Diagnostics: {string.Join("; ", diagnostics)}";

        return new SerialPortDiscoveryResult(
            SerialPortDiscoveryStatus.NotFound,
            null,
            $"No COM port produced valid LumaBloom telemetry. Checked ports: {string.Join(", ", checkedPorts)}.{diagnosticsSuffix}");
    }

    private static void ValidateArguments(
        int baudRate,
        int discoveryTimeoutMs,
        int readTimeoutMs,
        string newLine)
    {
        ArgumentException.ThrowIfNullOrEmpty(newLine);

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "BaudRate must be greater than zero.");
        }

        if (discoveryTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discoveryTimeoutMs),
                discoveryTimeoutMs,
                "DiscoveryTimeoutMs must be greater than zero.");
        }

        if (readTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readTimeoutMs),
                readTimeoutMs,
                "ReadTimeoutMs must be greater than zero.");
        }
    }

    private (bool IsMatch, string? Diagnostic) TryProbePort(
        string portName,
        TimeSpan discoveryTimeout,
        Func<SensorMessage, bool> predicate)
    {
        try
        {
            using var serialPort = new SerialPort(portName, _baudRate);
            serialPort.NewLine = _newLine;
            serialPort.ReadTimeout = _readTimeoutMs;

            try
            {
                serialPort.Open();
            }
            catch (Exception exception)
            {
                return (false, $"{portName}: open failed ({exception.Message})");
            }

            var deadline = DateTimeOffset.UtcNow.Add(discoveryTimeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                string line;

                try
                {
                    line = serialPort.ReadLine().Trim();
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (Exception exception)
                {
                    return (false, $"{portName}: read failed ({exception.Message})");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!SensorMessageParser.TryParse(line, out var message))
                {
                    continue;
                }

                if (predicate(message))
                {
                    return (true, null);
                }
            }

            return (false, null);
        }
        catch (Exception exception)
        {
            return (false, $"{portName}: discovery failed ({exception.Message})");
        }
    }
}
