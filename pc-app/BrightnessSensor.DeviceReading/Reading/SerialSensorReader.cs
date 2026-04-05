using System.IO.Ports;
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

    public SensorReadResult TryReadMessage()
    {
        try
        {
            var line = _serialPort.ReadLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return new SensorReadResult(SensorReadStatus.TimeoutOrEmpty, null, null, null);
            }

            if (!SensorMessageParser.TryParse(line, out var message))
            {
                return new SensorReadResult(SensorReadStatus.InvalidPayload, null, line, null);
            }

            return new SensorReadResult(SensorReadStatus.Success, message, line, null);
        }
        catch (TimeoutException)
        {
            return new SensorReadResult(SensorReadStatus.TimeoutOrEmpty, null, null, null);
        }
        catch (Exception exception)
        {
            return new SensorReadResult(SensorReadStatus.Error, null, null, exception.Message);
        }
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
