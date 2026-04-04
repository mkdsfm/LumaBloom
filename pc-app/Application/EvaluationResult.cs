namespace BrightnessSensor.ConsoleApp.Application;

// Processor output for one sensor sample.
internal readonly record struct EvaluationResult(
    bool ShouldApply,
    int TargetBrightness,
    double Normalized,
    double Filtered);
