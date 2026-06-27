using System.Text.Json.Serialization;

namespace BrightnessSensor.DeviceReading.Models;

public sealed class CalibrationResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("calibrated")]
    public bool Calibrated { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("normalizedOffset")]
    public double? NormalizedOffset { get; init; }
}
