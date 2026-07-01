using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal static class ResolvedSettingsFactory
{
    public static ResolvedAppSettings Create(AppConfig config, DeviceProfile profile)
    {
        var processing = new ProcessingSettings(
            AdcMin: config.Processing?.AdcMin ?? profile.Processing.AdcMin,
            AdcMax: config.Processing?.AdcMax ?? profile.Processing.AdcMax,
            Invert: config.Processing?.Invert ?? profile.Processing.Invert,
            EmaAlpha: config.Processing?.EmaAlpha ?? profile.Processing.EmaAlpha,
            HysteresisPercent: config.Processing?.HysteresisPercent ?? profile.Processing.HysteresisPercent,
            MaxBrightnessStepPercent: config.Processing?.MaxBrightnessStepPercent ?? profile.Processing.MaxBrightnessStepPercent,
            Gamma: config.Processing?.Gamma ?? profile.Processing.Gamma);

        var brightness = new BrightnessSettings(
            MinPercent: config.Brightness?.MinPercent ?? profile.Brightness.MinPercent,
            MaxPercent: config.Brightness?.MaxPercent ?? profile.Brightness.MaxPercent,
            Curve: HasUsableCurve(config.Brightness?.Curve)
                ? config.Brightness!.Curve!
                : profile.Brightness.Curve);

        var calibration = new CalibrationSettings(
            Enabled: config.Calibration?.Enabled ?? profile.Calibration.Enabled,
            SampleCount: config.Calibration?.SampleCount ?? profile.Calibration.SampleCount,
            MaxReadAttempts: config.Calibration?.MaxReadAttempts ?? profile.Calibration.MaxReadAttempts);

        var discoveryDeviceId = !string.IsNullOrWhiteSpace(config.Serial.DeviceId)
            ? config.Serial.DeviceId
            : profile.IsGeneric
                ? null
                : profile.DeviceId;

        var resolved = new ResolvedAppSettings(
            ProfileId: profile.ProfileId,
            MeasurementKind: profile.MeasurementKind,
            IsGenericProfile: profile.IsGeneric,
            DiscoveryDeviceId: discoveryDeviceId,
            BaudRate: config.Serial.BaudRate ?? profile.BaudRate,
            DiscoveryTimeoutMs: config.Serial.DiscoveryTimeoutMs ?? profile.DiscoveryTimeoutMs,
            Processing: processing,
            Brightness: brightness,
            Calibration: calibration);

        Validate(resolved);
        return resolved;
    }

    private static void Validate(ResolvedAppSettings settings)
    {
        if (settings.Processing.AdcMax <= settings.Processing.AdcMin)
        {
            throw new InvalidOperationException("processing.adcMax must be greater than processing.adcMin.");
        }

        if (settings.Processing.EmaAlpha is <= 0 or > 1)
        {
            throw new InvalidOperationException("processing.emaAlpha must be in the range (0, 1].");
        }

        if (settings.Processing.HysteresisPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("processing.hysteresisPercent must be in the range 0..100.");
        }

        if (settings.Processing.MaxBrightnessStepPercent is <= 0 or > 100)
        {
            throw new InvalidOperationException("processing.maxBrightnessStepPercent must be in the range 1..100.");
        }

        if (settings.Processing.Gamma is <= 0)
        {
            throw new InvalidOperationException("processing.gamma must be greater than 0 when specified.");
        }

        if (settings.Brightness.MinPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.minPercent must be in the range 0..100.");
        }

        if (settings.Brightness.MaxPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.maxPercent must be in the range 0..100.");
        }

        if (settings.Brightness.MinPercent > settings.Brightness.MaxPercent)
        {
            throw new InvalidOperationException(
                "brightness.minPercent cannot be greater than brightness.maxPercent.");
        }

        ValidateBrightnessCurve(settings.Brightness.Curve);

        if (settings.Calibration.SampleCount <= 0)
        {
            throw new InvalidOperationException("calibration.sampleCount must be greater than 0.");
        }

        if (settings.Calibration.MaxReadAttempts <= 0)
        {
            throw new InvalidOperationException("calibration.maxReadAttempts must be greater than 0.");
        }

        if (settings.Calibration.MaxReadAttempts < settings.Calibration.SampleCount)
        {
            throw new InvalidOperationException(
                "calibration.maxReadAttempts must be greater than or equal to calibration.sampleCount.");
        }
    }

    private static void ValidateBrightnessCurve(IReadOnlyList<BrightnessCurvePoint> curve)
    {
        if (curve.Count < 2)
        {
            throw new InvalidOperationException("brightness.curve must contain at least two points.");
        }

        var seen = new HashSet<int>();
        foreach (var point in curve)
        {
            if (point.LightPercent is < 0 or > 100)
            {
                throw new InvalidOperationException("brightness.curve lightPercent must be in the range 0..100.");
            }

            if (point.BrightnessPercent is < 0 or > 100)
            {
                throw new InvalidOperationException("brightness.curve brightnessPercent must be in the range 0..100.");
            }

            if (!seen.Add(point.LightPercent))
            {
                throw new InvalidOperationException("brightness.curve cannot contain duplicate lightPercent values.");
            }
        }
    }

    private static bool HasUsableCurve(IReadOnlyList<BrightnessCurvePoint>? curve)
    {
        return curve is { Count: >= 2 };
    }
}
