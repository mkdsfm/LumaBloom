using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Root configuration model loaded from appsettings.json.
internal sealed class AppConfig
{
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
    /// Runtime terminal UI preferences.
    /// </summary>
    [JsonPropertyName("ui")]
    public UiSettings Ui { get; init; } = new();
}
