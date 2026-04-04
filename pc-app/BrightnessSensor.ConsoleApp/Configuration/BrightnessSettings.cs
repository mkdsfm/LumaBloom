using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Target brightness bounds in percent.
internal sealed class BrightnessSettings
{
    /// <summary>
    /// Нижняя граница итоговой яркости монитора в процентах.
    /// Ограничивает минимальную яркость даже при очень низком освещении.
    /// </summary>
    [JsonPropertyName("minPercent")]
    public int MinPercent { get; init; }

    /// <summary>
    /// Верхняя граница итоговой яркости монитора в процентах.
    /// Ограничивает максимальную яркость даже при очень высоком освещении.
    /// </summary>
    [JsonPropertyName("maxPercent")]
    public int MaxPercent { get; init; }
}
