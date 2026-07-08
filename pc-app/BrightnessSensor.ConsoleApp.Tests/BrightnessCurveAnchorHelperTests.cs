using BrightnessSensor.ConsoleApp.Application;
using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class BrightnessCurveAnchorHelperTests
{
    [Fact]
    public void RebuildAnchoredCurve_AnchorsMiddlePoint_AndPreservesEndpoints()
    {
        var curve = CreateCurve((0, 10), (25, 28), (50, 55), (75, 78), (100, 100));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 16, 60);

        Assert.Equal(10, anchored[0].BrightnessPercent);
        Assert.Contains(anchored, point => point.LightPercent == 16 && point.BrightnessPercent == 60);
        Assert.Equal(60, BrightnessCurveAnchorHelper.GetCurveBrightness(anchored, 16));
        Assert.Equal(100, anchored[^1].BrightnessPercent);
    }

    [Fact]
    public void RebuildAnchoredCurve_HandlesAnchorNearZero()
    {
        var curve = CreateCurve((0, 10), (25, 28), (50, 55), (75, 78), (100, 100));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 5, 15);

        Assert.Contains(anchored, point => point.LightPercent == 5 && point.BrightnessPercent == 15);
        Assert.Equal(15, BrightnessCurveAnchorHelper.GetCurveBrightness(anchored, 5));
        Assert.Equal(10, anchored[0].BrightnessPercent);
        Assert.InRange(anchored[1].BrightnessPercent, 15, 100);
    }

    [Fact]
    public void RebuildAnchoredCurve_HandlesAnchorNearHundred()
    {
        var curve = CreateCurve((0, 10), (25, 28), (50, 55), (75, 78), (100, 100));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 95, 92);

        Assert.Contains(anchored, point => point.LightPercent == 95 && point.BrightnessPercent == 92);
        Assert.Equal(92, BrightnessCurveAnchorHelper.GetCurveBrightness(anchored, 95));
        Assert.Equal(10, anchored[0].BrightnessPercent);
        Assert.InRange(anchored[^2].BrightnessPercent, 0, 92);
        Assert.Equal(100, anchored[^1].BrightnessPercent);
    }

    [Fact]
    public void RebuildAnchoredCurve_UsesLinearFallback_WhenLeftSegmentIsFlat()
    {
        var curve = CreateCurve((0, 20), (25, 20), (50, 50), (75, 80), (100, 100));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 10, 40);

        Assert.Contains(anchored, point => point.LightPercent == 10 && point.BrightnessPercent == 40);
        Assert.Equal(40, BrightnessCurveAnchorHelper.GetCurveBrightness(anchored, 10));
        Assert.Equal(20, anchored[0].BrightnessPercent);
        Assert.InRange(anchored[1].BrightnessPercent, 20, 100);
    }

    [Fact]
    public void RebuildAnchoredCurve_UsesLinearFallback_WhenRightSegmentIsFlat()
    {
        var curve = CreateCurve((0, 10), (25, 30), (50, 70), (75, 100), (100, 100));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 90, 85);

        Assert.Contains(anchored, point => point.LightPercent == 90 && point.BrightnessPercent == 85);
        Assert.Equal(85, BrightnessCurveAnchorHelper.GetCurveBrightness(anchored, 90));
        Assert.InRange(anchored[^2].BrightnessPercent, 0, 100);
        Assert.Equal(100, anchored[^1].BrightnessPercent);
    }

    [Fact]
    public void RebuildAnchoredCurve_ClampsBrightnessValues()
    {
        var curve = CreateCurve((0, 0), (25, 5), (50, 10), (75, 15), (100, 20));

        var anchored = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(curve, 50, 100);

        Assert.All(anchored, point => Assert.InRange(point.BrightnessPercent, 0, 100));
    }

    [Fact]
    public void TryGetAmbientPercent_UsesRawAdcSettings()
    {
        var settings = CreateSettings(MeasurementKind.Adc, 200, 3200, invert: true);
        var sensor = new SensorRuntimeSnapshot("esp32c6-01", "light0", 1, 2600, DateTimeOffset.Now);

        var success = BrightnessCurveAnchorHelper.TryGetAmbientPercent(sensor, settings, out var ambientPercent);

        Assert.True(success);
        Assert.Equal(20, ambientPercent);
    }

    private static IReadOnlyList<BrightnessCurvePoint> CreateCurve(params (int Light, int Brightness)[] points)
    {
        return points.Select(point => new BrightnessCurvePoint(point.Light, point.Brightness)).ToArray();
    }

    private static ResolvedAppSettings CreateSettings(MeasurementKind kind, int adcMin, int adcMax, bool invert)
    {
        return new ResolvedAppSettings(
            "test",
            kind,
            false,
            null,
            115200,
            3000,
            new ProcessingSettings(adcMin, adcMax, invert, 0.2, 1, 2, 1.0),
            new BrightnessSettings(10, 100, CreateCurve((0, 10), (25, 28), (50, 55), (75, 78), (100, 100))),
            new CalibrationSettings(false, 5, 20));
    }
}
