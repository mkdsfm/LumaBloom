using System.Text.Json.Serialization;

namespace BrightnessSensor.DeviceReading;

public sealed class SensorMessage
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = string.Empty;

    [JsonPropertyName("sensorId")]
    public string SensorId { get; init; } = string.Empty;

    [JsonPropertyName("ts")]
    public long Timestamp { get; init; }

    [JsonPropertyName("value")]
    public int Value { get; init; }
}
