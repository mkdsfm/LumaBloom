namespace BrightnessSensor.BrightnessMath;

public readonly record struct EvaluationResult(
    bool ShouldApply,
    int TargetBrightness,
    double Normalized,
    double Filtered);
