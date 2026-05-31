using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class BrightnessOverrides
{
    [JsonPropertyName("minPercent")]
    public int? MinPercent { get; init; }

    [JsonPropertyName("maxPercent")]
    public int? MaxPercent { get; init; }
}
