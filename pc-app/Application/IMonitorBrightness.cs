namespace BrightnessSensor.ConsoleApp.Application;

internal interface IMonitorBrightness
{
    string Source { get; }

    string Name { get; }

    bool TryGetBrightness(out int brightnessPercent, out string? error);

    bool TrySetBrightness(int brightnessPercent, out string? error);
}
