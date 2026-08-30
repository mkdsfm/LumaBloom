namespace BrightnessSensor.ConsoleApp.Application;

internal sealed record BundledFirmwareInfo(
    string Version,
    string FlashMethod,
    string Chip,
    int BaudRate,
    string Offset,
    string FileName,
    string AbsolutePath);
