namespace BrightnessSensor.BrightnessMath;

public sealed record BrightnessComputationSettings(
    bool InputIsNormalized1000,
    int AdcMin,
    int AdcMax,
    bool Invert,
    double EmaAlpha,
    int HysteresisPercent,
    int MaxBrightnessStepPercent,
    double? Gamma,
    int MinBrightnessPercent,
    int MaxBrightnessPercent,
    IReadOnlyList<BrightnessCurvePointSetting>? BrightnessCurve = null);
