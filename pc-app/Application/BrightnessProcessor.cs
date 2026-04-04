using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Application;

// Converts raw ADC values to target brightness using normalize + EMA + hysteresis.
internal sealed class BrightnessProcessor(
    ProcessingSettings processingSettings,
    BrightnessSettings brightnessSettings)
{
    private double? _emaValue;
    private int? _lastAppliedBrightness;
    private double _normalizedOffset;
    private bool _hasCalibration;

    public EvaluationResult Evaluate(int rawAdcValue)
    {
        var normalized = Normalize(rawAdcValue);

        if (processingSettings.Invert)
        {
            normalized = 1.0 - normalized;
        }

        if (_hasCalibration)
        {
            normalized = Math.Clamp(normalized + _normalizedOffset, 0.0, 1.0);
        }

        _emaValue ??= normalized;
        _emaValue = (processingSettings.EmaAlpha * normalized) +
            ((1.0 - processingSettings.EmaAlpha) * _emaValue.Value);
        
        var effectiveValue = processingSettings.Gamma is null
            ? _emaValue.Value
            : Math.Pow(_emaValue.Value, processingSettings.Gamma.Value);

        var targetBrightness = (int)Math.Round(
            brightnessSettings.MinPercent +
            (effectiveValue * (brightnessSettings.MaxPercent - brightnessSettings.MinPercent)),
            MidpointRounding.AwayFromZero);

        targetBrightness = Math.Clamp(
            targetBrightness,
            brightnessSettings.MinPercent,
            brightnessSettings.MaxPercent);

        if (_lastAppliedBrightness.HasValue &&
            Math.Abs(targetBrightness - _lastAppliedBrightness.Value) <
            processingSettings.HysteresisPercent)
        {
            return new EvaluationResult(
                ShouldApply: false,
                TargetBrightness: targetBrightness,
                Normalized: normalized,
                Filtered: _emaValue.Value);
        }

        _lastAppliedBrightness = targetBrightness;
        return new EvaluationResult(
            ShouldApply: true,
            TargetBrightness: targetBrightness,
            Normalized: normalized,
            Filtered: _emaValue.Value);
    }

    public bool TryCalibrate(int rawAdcValue, int currentBrightnessPercent, out string? error)
    {
        error = null;

        if (currentBrightnessPercent is < 0 or > 100)
        {
            error = "Current brightness percent must be in range 0..100.";
            return false;
        }

        var expectedBrightness = Math.Clamp(
            currentBrightnessPercent,
            brightnessSettings.MinPercent,
            brightnessSettings.MaxPercent);

        var expectedEffective = (expectedBrightness - brightnessSettings.MinPercent) /
            (double)(brightnessSettings.MaxPercent - brightnessSettings.MinPercent);

        expectedEffective = Math.Clamp(expectedEffective, 0.0, 1.0);

        var expectedPreGamma = processingSettings.Gamma is null
            ? expectedEffective
            : Math.Pow(expectedEffective, 1.0 / processingSettings.Gamma.Value);

        var normalized = Normalize(rawAdcValue);
        if (processingSettings.Invert)
        {
            normalized = 1.0 - normalized;
        }

        _normalizedOffset = expectedPreGamma - normalized;
        _hasCalibration = true;

        // Seed EMA and last applied value so the first update doesn't jump away from the baseline.
        _emaValue = expectedPreGamma;
        _lastAppliedBrightness = expectedBrightness;

        return true;
    }

    private double Normalize(int rawAdcValue)
    {
        var clampedAdcValue = Math.Clamp(
            rawAdcValue,
            processingSettings.AdcMin,
            processingSettings.AdcMax);

        return (clampedAdcValue - processingSettings.AdcMin) /
            (double)(processingSettings.AdcMax - processingSettings.AdcMin);
    }
}
