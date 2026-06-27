using System.Text.Json;

namespace BrightnessSensor.DeviceReading.Models;

public static class CalibrationResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse(string line, out CalibrationResponse response)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<CalibrationResponse>(line, JsonOptions);
            if (parsed is null || !string.Equals(parsed.Type, "calibrationResult", StringComparison.OrdinalIgnoreCase))
            {
                response = new CalibrationResponse();
                return false;
            }

            response = parsed;
            return true;
        }
        catch (JsonException)
        {
            response = new CalibrationResponse();
            return false;
        }
        catch (NotSupportedException)
        {
            response = new CalibrationResponse();
            return false;
        }
    }
}
