namespace BrightnessSensor.BrightnessMath;

public sealed record BrightnessComputationSettings(
    int AdcMin,
    int AdcMax,
    bool Invert,
    double EmaAlpha,
    int HysteresisPercent,
    double? Gamma,
    int MinBrightnessPercent,
    int MaxBrightnessPercent);
