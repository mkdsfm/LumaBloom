namespace BrightnessSensor.ConsoleApp.Configuration;

// Concrete processing parameters used after resolving a hardware profile.
internal sealed record ProcessingSettings(
    int AdcMin,
    int AdcMax,
    bool Invert,
    double EmaAlpha,
    int HysteresisPercent,
    int MaxBrightnessStepPercent,
    double? Gamma);
