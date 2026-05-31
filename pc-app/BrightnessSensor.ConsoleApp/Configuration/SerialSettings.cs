using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Serial discovery and communication settings for reading telemetry from ESP32.
internal sealed class SerialSettings
{
    public const int DefaultBaudRate = 115200;
    public const int DefaultDiscoveryTimeoutMs = 2500;

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("baudRate")]
    public int? BaudRate { get; init; }

    [JsonPropertyName("discoveryTimeoutMs")]
    public int? DiscoveryTimeoutMs { get; init; }
}
