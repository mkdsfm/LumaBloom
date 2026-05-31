using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class DeviceProfileSelectionSettings
{
    [JsonPropertyName("autoDetect")]
    public bool AutoDetect { get; init; } = true;

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }
}
