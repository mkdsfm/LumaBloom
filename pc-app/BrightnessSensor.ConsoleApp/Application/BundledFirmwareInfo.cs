namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record BundledFirmwareInfo(
    string Version,
    string Chip,
    string Board,
    string FileName,
    string AbsolutePath);
