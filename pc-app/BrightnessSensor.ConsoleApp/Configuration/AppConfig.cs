using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Root configuration model loaded from appsettings.json.
internal sealed class AppConfig
{
    /// <summary>
    /// COM port connection parameters used to read sensor telemetry.
    /// </summary>
    [JsonPropertyName("serial")]
    public required SerialSettings Serial { get; init; }

    /// <summary>
    /// Signal processing parameters used to convert ADC values to brightness.
    /// </summary>
    [JsonPropertyName("processing")]
    public required ProcessingSettings Processing { get; init; }

    /// <summary>
    /// Output brightness limits applied to the final value.
    /// </summary>
    [JsonPropertyName("brightness")]
    public required BrightnessSettings Brightness { get; init; }

    /// <summary>
    /// Startup calibration parameters.
    /// </summary>
    [JsonPropertyName("calibration")]
    public CalibrationSettings Calibration { get; init; } = new();
}
