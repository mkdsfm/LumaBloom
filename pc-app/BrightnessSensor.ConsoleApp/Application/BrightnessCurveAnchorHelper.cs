using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;

namespace BrightnessSensor.ConsoleApp.Application;

internal static class BrightnessCurveAnchorHelper
{
    public static bool TryGetAmbientPercent(
        SensorRuntimeSnapshot? latestSensor,
        ResolvedAppSettings settings,
        out int ambientPercent)
    {
        ambientPercent = 0;
        if (latestSensor is null)
        {
            return false;
        }

        if (!latestSensor.Raw.HasValue)
        {
            return false;
        }

        var measurementValue = latestSensor.Raw.Value;
        var range = settings.Processing.AdcMax - settings.Processing.AdcMin;
        if (range <= 0)
        {
            return false;
        }

        var clamped = Math.Clamp(measurementValue, settings.Processing.AdcMin, settings.Processing.AdcMax);
        var normalized = (clamped - settings.Processing.AdcMin) / (double)range;
        if (settings.Processing.Invert)
        {
            normalized = 1.0 - normalized;
        }

        ambientPercent = (int)Math.Round(
            Math.Clamp(normalized, 0.0, 1.0) * 100.0,
            MidpointRounding.AwayFromZero);
        return true;
    }

    public static int GetCurveBrightness(IReadOnlyList<BrightnessCurvePoint> curve, int lightPercent)
    {
        var sorted = curve.OrderBy(point => point.LightPercent).ToArray();
        if (sorted.Length == 0)
        {
            return lightPercent;
        }

        if (lightPercent <= sorted[0].LightPercent)
        {
            return sorted[0].BrightnessPercent;
        }

        if (lightPercent >= sorted[^1].LightPercent)
        {
            return sorted[^1].BrightnessPercent;
        }

        for (var i = 0; i < sorted.Length - 1; i++)
        {
            var left = sorted[i];
            var right = sorted[i + 1];
            if (lightPercent < left.LightPercent || lightPercent > right.LightPercent)
            {
                continue;
            }

            var span = right.LightPercent - left.LightPercent;
            if (span <= 0)
            {
                return right.BrightnessPercent;
            }

            var ratio = (lightPercent - left.LightPercent) / (double)span;
            return (int)Math.Round(
                left.BrightnessPercent + (ratio * (right.BrightnessPercent - left.BrightnessPercent)),
                MidpointRounding.AwayFromZero);
        }

        return sorted[^1].BrightnessPercent;
    }

    public static IReadOnlyList<BrightnessCurvePoint> RebuildAnchoredCurve(
        IReadOnlyList<BrightnessCurvePoint> curve,
        int ambientPercent,
        int desiredBrightnessPercent)
    {
        var sorted = curve.OrderBy(point => point.LightPercent).ToArray();
        if (sorted.Length == 0)
        {
            return [];
        }

        var anchorX = Math.Clamp(ambientPercent, 0, 100);
        var anchorY = Math.Clamp(desiredBrightnessPercent, 0, 100);
        var y0 = GetCurveBrightness(sorted, 0);
        var yAnchor = GetCurveBrightness(sorted, anchorX);
        var y100 = GetCurveBrightness(sorted, 100);

        var rebuiltPoints = sorted
            .Select(point =>
            {
                var oldY = point.BrightnessPercent;
                double newY;

                if (point.LightPercent <= anchorX)
                {
                    if (yAnchor == y0)
                    {
                        newY = InterpolateLinearly(0, y0, anchorX, anchorY, point.LightPercent);
                    }
                    else
                    {
                        newY = y0 + ((oldY - y0) * (anchorY - y0) / (double)(yAnchor - y0));
                    }
                }
                else if (y100 == yAnchor)
                {
                    newY = InterpolateLinearly(anchorX, anchorY, 100, y100, point.LightPercent);
                }
                else
                {
                    newY = y100 - ((y100 - oldY) * (y100 - anchorY) / (double)(y100 - yAnchor));
                }

                return new BrightnessCurvePoint(
                    point.LightPercent,
                    Math.Clamp((int)Math.Round(newY, MidpointRounding.AwayFromZero), 0, 100));
            })
            .ToArray();

        // Persist the exact live anchor so interpolation at the current light level
        // evaluates to the requested brightness instead of only approximating it
        // through the surrounding fixed curve points.
        return rebuiltPoints
            .Where(point => point.LightPercent != anchorX)
            .Append(new BrightnessCurvePoint(anchorX, anchorY))
            .OrderBy(point => point.LightPercent)
            .ToArray();
    }

    private static double InterpolateLinearly(int x0, int y0, int x1, int y1, int x)
    {
        if (x1 == x0)
        {
            return y1;
        }

        var ratio = (x - x0) / (double)(x1 - x0);
        return y0 + (ratio * (y1 - y0));
    }
}
