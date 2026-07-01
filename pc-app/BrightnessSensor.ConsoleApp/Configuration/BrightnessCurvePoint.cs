using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed record BrightnessCurvePoint(
    [property: JsonPropertyName("lightPercent")] int LightPercent,
    [property: JsonPropertyName("brightnessPercent")] int BrightnessPercent);
