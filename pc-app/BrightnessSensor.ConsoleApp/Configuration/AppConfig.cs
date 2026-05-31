using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Root configuration model loaded from appsettings.json.
internal sealed class AppConfig
{
    /// <summary>
    /// COM port connection parameters used to read sensor telemetry.
    /// </summary>
    [JsonPropertyName("serial")]
    public SerialSettings Serial { get; init; } = new();

    /// <summary>
    /// Hardware profile selection behavior.
    /// </summary>
    [JsonPropertyName("deviceProfile")]
    public DeviceProfileSelectionSettings DeviceProfile { get; init; } = new();

    /// <summary>
    /// Optional signal processing overrides applied on top of a resolved hardware profile.
    /// </summary>
    [JsonPropertyName("processing")]
    public ProcessingOverrides? Processing { get; init; }

    /// <summary>
    /// Optional output brightness overrides applied to the final value.
    /// </summary>
    [JsonPropertyName("brightness")]
    public BrightnessOverrides? Brightness { get; init; }

    /// <summary>
    /// Optional startup calibration overrides.
    /// </summary>
    [JsonPropertyName("calibration")]
    public CalibrationOverrides? Calibration { get; init; }
}
