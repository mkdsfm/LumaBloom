namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record FirmwareArtifactManifest(
    int SchemaVersion,
    string Version,
    string FileName,
    string Variant,
    string Board,
    string FlashMethod,
    string Chip,
    int BaudRate,
    string Offset);
