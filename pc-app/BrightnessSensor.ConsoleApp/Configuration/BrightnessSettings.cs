namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed record BrightnessSettings(
    int MinPercent,
    int MaxPercent,
    IReadOnlyList<BrightnessCurvePoint> Curve);
