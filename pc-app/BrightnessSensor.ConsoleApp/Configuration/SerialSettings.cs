using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Serial communication settings for reading telemetry from ESP32.
internal sealed class SerialSettings
{
    /// <summary>
    /// Имя COM-порта, из которого читается телеметрия датчика (например, COM5).
    /// Влияет на результат напрямую: если указан неверный порт, приложение не получит данные
    /// и яркость не будет обновляться.
    /// </summary>
    [JsonPropertyName("portName")]
    public required string PortName { get; init; }

    /// <summary>
    /// Скорость последовательного порта.
    /// Должна совпадать со скоростью в прошивке: при несовпадении возможны ошибки чтения
    /// или некорректный поток данных, что ухудшит/остановит расчет яркости.
    /// </summary>
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; init; } = 115200;
}
