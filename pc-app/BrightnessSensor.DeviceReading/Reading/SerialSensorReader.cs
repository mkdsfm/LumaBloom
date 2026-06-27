using System.IO.Ports;
using System.Text.Json;
using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.DeviceReading.Reading;

public sealed class SerialSensorReader(string portName, int baudRate, int readTimeoutMs = 1500, string newLine = "\n")
    : IDisposable
{
    private readonly SerialPort _serialPort = CreateSerialPort(portName, baudRate, readTimeoutMs, newLine);

    public void Open()
    {
        _serialPort.Open();
    }

    public bool TryWriteLine<T>(T payload, out string? error)
    {
        try
        {
            var line = JsonSerializer.Serialize(payload);
            _serialPort.WriteLine(line);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public SerialLineReadResult TryReadLine()
    {
        try
        {
            var line = _serialPort.ReadLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return new SerialLineReadResult(SensorReadStatus.TimeoutOrEmpty, null, null);
            }

            return new SerialLineReadResult(SensorReadStatus.Success, line, null);
        }
        catch (TimeoutException)
        {
            return new SerialLineReadResult(SensorReadStatus.TimeoutOrEmpty, null, null);
        }
        catch (Exception exception)
        {
            return new SerialLineReadResult(SensorReadStatus.Error, null, exception.Message);
        }
    }

    public SensorReadResult TryReadMessage()
    {
        var lineResult = TryReadLine();
        if (lineResult.Status == SensorReadStatus.TimeoutOrEmpty || lineResult.Status == SensorReadStatus.Error)
        {
            return new SensorReadResult(lineResult.Status, null, null, lineResult.Error);
        }

        if (!SensorMessageParser.TryParse(lineResult.Line!, out var message))
        {
            return new SensorReadResult(SensorReadStatus.InvalidPayload, null, lineResult.Line, null);
        }

        return new SensorReadResult(SensorReadStatus.Success, message, lineResult.Line, null);
    }

    public void Dispose()
    {
        _serialPort.Dispose();
    }

    private static SerialPort CreateSerialPort(
        string portName,
        int baudRate,
        int readTimeoutMs,
        string newLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentException.ThrowIfNullOrEmpty(newLine);

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "Baud rate must be greater than zero.");
        }

        if (readTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(readTimeoutMs), readTimeoutMs, "Read timeout must be greater than zero.");
        }

        return new SerialPort(portName, baudRate)
        {
            NewLine = newLine,
            ReadTimeout = readTimeoutMs
        };
    }
}
