using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal static class ResolvedSettingsFactory
{
    private const int DefaultBaudRate = 115200;
    private const int DefaultDiscoveryTimeoutMs = 2500;
    private const int DefaultMinBrightnessPercent = 10;
    private const int DefaultMaxBrightnessPercent = 100;

    public static ResolvedAppSettings Create(AppConfig config)
    {
        var minBrightnessPercent = config.Brightness?.MinPercent ?? DefaultMinBrightnessPercent;
        var maxBrightnessPercent = config.Brightness?.MaxPercent ?? DefaultMaxBrightnessPercent;

        var processing = new ProcessingSettings(
            AdcMin: config.Processing?.AdcMin ?? 200,
            AdcMax: config.Processing?.AdcMax ?? 3200,
            Invert: config.Processing?.Invert ?? true,
            EmaAlpha: config.Processing?.EmaAlpha ?? 0.2,
            HysteresisPercent: config.Processing?.HysteresisPercent ?? 1,
            MaxBrightnessStepPercent: config.Processing?.MaxBrightnessStepPercent ?? 2,
            Gamma: config.Processing?.Gamma ?? 1.0);

        var brightness = new BrightnessSettings(
            MinPercent: minBrightnessPercent,
            MaxPercent: maxBrightnessPercent,
            Curve: HasUsableCurve(config.Brightness?.Curve)
                ? config.Brightness!.Curve!
                : CreateDefaultCurve(minBrightnessPercent, maxBrightnessPercent));

        var resolved = new ResolvedAppSettings(
            ProtocolId: LumaBloomTelemetry.ProtocolId,
            MeasurementKind: MeasurementKind.Adc,
            BaudRate: config.Connection?.BaudRate ?? DefaultBaudRate,
            DiscoveryTimeoutMs: config.Connection?.DiscoveryTimeoutMs ?? DefaultDiscoveryTimeoutMs,
            Processing: processing,
            Brightness: brightness);

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

    private static IReadOnlyList<BrightnessCurvePoint> CreateDefaultCurve(int minPercent, int maxPercent)
    {
        return
        [
            new BrightnessCurvePoint(0, minPercent),
            new BrightnessCurvePoint(25, Interpolate(minPercent, maxPercent, 0.25)),
            new BrightnessCurvePoint(50, Interpolate(minPercent, maxPercent, 0.50)),
            new BrightnessCurvePoint(75, Interpolate(minPercent, maxPercent, 0.75)),
            new BrightnessCurvePoint(100, maxPercent)
        ];
    }

    private static int Interpolate(int minPercent, int maxPercent, double ratio)
    {
        return (int)Math.Round(minPercent + ((maxPercent - minPercent) * ratio), MidpointRounding.AwayFromZero);
    }
}
