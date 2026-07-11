namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record BundledFirmwareSnapshot(
    string Version,
    string FileName,
    string StatusMessage,
    bool IsAvailable,
    bool IsBusy);
