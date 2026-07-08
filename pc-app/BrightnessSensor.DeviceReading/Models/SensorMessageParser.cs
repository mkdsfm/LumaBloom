using System.Text.Json;

namespace BrightnessSensor.DeviceReading.Models;

public static class SensorMessageParser
{
    public static bool TryParse(string line, out SensorMessage message)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                message = new SensorMessage();
                return false;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String)
            {
                message = new SensorMessage();
                return false;
            }

            var id = idElement.GetString();
            if (!LumaBloomTelemetry.HasSupportedProtocolId(id))
            {
                message = new SensorMessage();
                return false;
            }

            if (!root.TryGetProperty("ts", out var timestampElement) ||
                timestampElement.ValueKind != JsonValueKind.Number ||
                !timestampElement.TryGetInt64(out var timestamp))
            {
                message = new SensorMessage();
                return false;
            }

            if (!root.TryGetProperty("raw", out var rawElement) ||
                rawElement.ValueKind != JsonValueKind.Number ||
                !rawElement.TryGetInt32(out var raw))
            {
                message = new SensorMessage();
                return false;
            }

            message = new SensorMessage
            {
                Id = id!,
                Timestamp = timestamp,
                Raw = raw
            };
            return true;
        }
        catch (JsonException)
        {
            message = new SensorMessage();
            return false;
        }
        catch (NotSupportedException)
        {
            message = new SensorMessage();
            return false;
        }
    }
}
