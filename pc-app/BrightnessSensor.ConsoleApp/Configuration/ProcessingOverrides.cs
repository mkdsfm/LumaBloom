using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class ProcessingOverrides
{
    [JsonPropertyName("adcMin")]
    public int? AdcMin { get; init; }

    [JsonPropertyName("adcMax")]
    public int? AdcMax { get; init; }

    [JsonPropertyName("invert")]
    public bool? Invert { get; init; }

    [JsonPropertyName("emaAlpha")]
    public double? EmaAlpha { get; init; }

    [JsonPropertyName("hysteresisPercent")]
    public int? HysteresisPercent { get; init; }

    [JsonPropertyName("gamma")]
    public double? Gamma { get; init; }
}
