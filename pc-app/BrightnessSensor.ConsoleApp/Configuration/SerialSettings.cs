using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Serial discovery and communication settings for reading telemetry from ESP32.
internal sealed class SerialSettings
{
    /// <summary>
    /// Имя COM-порта, из которого читается телеметрия датчика (например, COM5).
    /// Влияет на результат напрямую: если указан неверный порт, приложение не получит данные
    /// и яркость не будет обновляться.
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Скорость последовательного порта.
    /// Должна совпадать со скоростью в прошивке: при несовпадении возможны ошибки чтения
    /// или некорректный поток данных, что ухудшит/остановит расчет яркости.
    /// </summary>
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; init; } = 115200;

    /// <summary>
    /// Maximum time spent probing one COM port during discovery.
    /// </summary>
    [JsonPropertyName("discoveryTimeoutMs")]
    public int DiscoveryTimeoutMs { get; init; } = 2500;
}
