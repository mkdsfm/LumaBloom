using BrightnessSensor.BrightnessMath;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class BrightnessProcessorTests
{
    [Fact]
    public void Evaluate_LimitsBrightnessStep_WhenTargetChangesAbruptly()
    {
        var processor = CreateProcessor();

        var initial = processor.Evaluate(0);
        var jumped = processor.Evaluate(1000);

        Assert.True(initial.ShouldApply);
        Assert.Equal(10, initial.TargetBrightness);
        Assert.True(jumped.ShouldApply);
        Assert.Equal(100, jumped.RequestedBrightness);
        Assert.Equal(12, jumped.TargetBrightness);
    }

    [Fact]
    public void Evaluate_SkipsUpdates_InsideHysteresisWindow()
    {
        var processor = CreateProcessor();

        processor.Evaluate(500);
        var result = processor.Evaluate(503);

        Assert.False(result.ShouldApply);
        Assert.Equal(result.TargetBrightness, result.RequestedBrightness);
    }

    private static BrightnessProcessor CreateProcessor()
    {
        return new BrightnessProcessor(
            new BrightnessComputationSettings(
                InputIsNormalized1000: true,
                AdcMin: 0,
                AdcMax: 1000,
                Invert: false,
                EmaAlpha: 1.0,
                HysteresisPercent: 1,
                MaxBrightnessStepPercent: 2,
                Gamma: 1.0,
                MinBrightnessPercent: 10,
                MaxBrightnessPercent: 100));
    }
}
