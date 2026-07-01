using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed class UiSettings
{
    public const string AutoLanguage = "auto";
    public const string EnglishLanguage = "en";

    [JsonPropertyName("language")]
    public string Language { get; init; } = AutoLanguage;
}
