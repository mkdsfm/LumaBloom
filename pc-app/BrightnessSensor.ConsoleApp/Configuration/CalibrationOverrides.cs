using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class CalibrationOverrides
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("sampleCount")]
    public int? SampleCount { get; init; }

    [JsonPropertyName("maxReadAttempts")]
    public int? MaxReadAttempts { get; init; }
}
