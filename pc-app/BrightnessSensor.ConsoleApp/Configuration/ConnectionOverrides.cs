using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class ConnectionOverrides
{
    [JsonPropertyName("baudRate")]
    public int? BaudRate { get; init; }

    [JsonPropertyName("discoveryTimeoutMs")]
    public int? DiscoveryTimeoutMs { get; init; }
}
