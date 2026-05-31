namespace BrightnessSensor.ConsoleApp.Configuration;

internal sealed record CalibrationSettings(
    bool Enabled,
    int SampleCount,
    int MaxReadAttempts);
