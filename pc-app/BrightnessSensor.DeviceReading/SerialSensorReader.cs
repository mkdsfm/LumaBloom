using System.IO.Ports;

namespace BrightnessSensor.DeviceReading;

public sealed class SerialSensorReader(string portName, int baudRate, int readTimeoutMs = 1500, string newLine = "\n")
    : IDisposable
{
    private readonly SerialPort _serialPort = new(portName, baudRate)
    {
        NewLine = newLine,
        ReadTimeout = readTimeoutMs
    };

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
}
