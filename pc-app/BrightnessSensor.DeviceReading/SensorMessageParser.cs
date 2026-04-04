using System.Text.Json;

namespace BrightnessSensor.DeviceReading;

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
        catch
        {
            message = new SensorMessage();
            return false;
        }
    }
}
