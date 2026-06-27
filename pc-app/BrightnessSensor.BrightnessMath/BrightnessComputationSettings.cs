namespace BrightnessSensor.BrightnessMath;

public sealed record BrightnessComputationSettings(
    bool InputIsNormalized1000,
    int AdcMin,
    int AdcMax,
    bool Invert,
    double EmaAlpha,
    int HysteresisPercent,
    double? Gamma,
    int MinBrightnessPercent,
    int MaxBrightnessPercent);
