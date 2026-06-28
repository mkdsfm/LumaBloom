namespace BrightnessSensor.BrightnessMath;

public sealed class BrightnessProcessor(BrightnessComputationSettings settings)
{
    private double? _emaValue;
    private int? _lastAppliedBrightness;
    private double _normalizedOffset;
    private bool _hasCalibration;

    public EvaluationResult Evaluate(int rawAdcValue)
    {
        var normalized = Normalize(rawAdcValue);

        if (settings.Invert)
        {
            normalized = 1.0 - normalized;
        }

        if (_hasCalibration)
        {
            normalized = Math.Clamp(normalized + _normalizedOffset, 0.0, 1.0);
        }

        _emaValue ??= normalized;
        _emaValue = (settings.EmaAlpha * normalized) +
            ((1.0 - settings.EmaAlpha) * _emaValue.Value);

        var effectiveValue = settings.Gamma is null
            ? _emaValue.Value
            : Math.Pow(_emaValue.Value, settings.Gamma.Value);

        var requestedBrightness = (int)Math.Round(
            settings.MinBrightnessPercent +
            (effectiveValue * (settings.MaxBrightnessPercent - settings.MinBrightnessPercent)),
            MidpointRounding.AwayFromZero);

        requestedBrightness = Math.Clamp(
            requestedBrightness,
            settings.MinBrightnessPercent,
            settings.MaxBrightnessPercent);

        if (_lastAppliedBrightness.HasValue &&
            Math.Abs(requestedBrightness - _lastAppliedBrightness.Value) < settings.HysteresisPercent)
        {
            return new EvaluationResult(
                ShouldApply: false,
                RequestedBrightness: requestedBrightness,
                TargetBrightness: _lastAppliedBrightness.Value,
                Normalized: normalized,
                Filtered: _emaValue.Value);
        }

        var targetBrightness = requestedBrightness;
        if (_lastAppliedBrightness.HasValue)
        {
            var delta = requestedBrightness - _lastAppliedBrightness.Value;
            if (Math.Abs(delta) > settings.MaxBrightnessStepPercent)
            {
                targetBrightness = _lastAppliedBrightness.Value +
                    Math.Sign(delta) * settings.MaxBrightnessStepPercent;
            }
        }

        _lastAppliedBrightness = targetBrightness;
        return new EvaluationResult(
            ShouldApply: true,
            RequestedBrightness: requestedBrightness,
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
            settings.MinBrightnessPercent,
            settings.MaxBrightnessPercent);

        var brightnessRange = settings.MaxBrightnessPercent - settings.MinBrightnessPercent;
        if (brightnessRange <= 0)
        {
            error = "Brightness range must be greater than 0.";
            return false;
        }

        var expectedEffective = (expectedBrightness - settings.MinBrightnessPercent) / (double)brightnessRange;
        expectedEffective = Math.Clamp(expectedEffective, 0.0, 1.0);

        var expectedPreGamma = settings.Gamma is null
            ? expectedEffective
            : Math.Pow(expectedEffective, 1.0 / settings.Gamma.Value);

        var normalized = Normalize(rawAdcValue);
        if (settings.Invert)
        {
            normalized = 1.0 - normalized;
        }

        _normalizedOffset = expectedPreGamma - normalized;
        _hasCalibration = true;
        _emaValue = expectedPreGamma;
        _lastAppliedBrightness = expectedBrightness;

        return true;
    }

    private double Normalize(int rawAdcValue)
    {
        if (settings.InputIsNormalized1000)
        {
            var clampedNormalized = Math.Clamp(rawAdcValue, 0, 1000);
            return clampedNormalized / 1000.0;
        }

        var clampedAdcValue = Math.Clamp(rawAdcValue, settings.AdcMin, settings.AdcMax);
        return (clampedAdcValue - settings.AdcMin) / (double)(settings.AdcMax - settings.AdcMin);
    }
}
