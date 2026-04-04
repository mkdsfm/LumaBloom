using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Startup calibration parameters: baseline alignment between screen brightness and sensor.
internal sealed class CalibrationSettings
{
    /// <summary>
    /// Включает калибровку при запуске приложения.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Количество валидных измерений датчика для усреднения.
    /// </summary>
    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; init; } = 5;

    /// <summary>
    /// Максимальное число попыток чтения (включая таймауты/битые строки).
    /// </summary>
    [JsonPropertyName("maxReadAttempts")]
    public int MaxReadAttempts { get; init; } = 20;
}
