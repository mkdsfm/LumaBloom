using System.Text.Json.Serialization;

namespace BrightnessSensor.DeviceReading.Models;

public sealed class CalibrationCommand
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "calibrate";

    [JsonPropertyName("screenBrightnessPercent")]
    public int ScreenBrightnessPercent { get; init; }

    [JsonPropertyName("sensorAverageRaw")]
    public int SensorAverageRaw { get; init; }
}
