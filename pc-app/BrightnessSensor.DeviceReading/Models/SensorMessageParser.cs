using System.Text.Json;

namespace BrightnessSensor.DeviceReading.Models;

public static class SensorMessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse(string line, out SensorMessage message)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<SensorMessage>(line, JsonOptions);
            if (parsed is null)
            {
                message = new SensorMessage();
                return false;
            }

            message = parsed;
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
