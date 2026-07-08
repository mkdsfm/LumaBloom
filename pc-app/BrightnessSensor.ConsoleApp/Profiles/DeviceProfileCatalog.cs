using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal static class DeviceProfileCatalog
{
    public static readonly DeviceProfile Generic = new(
        ProfileId: "generic-adc-safe",
        DeviceId: "*",
        SensorId: "*",
        MeasurementKind: MeasurementKind.Adc,
        Processing: new ProcessingSettings(
            AdcMin: 0,
            AdcMax: 4095,
            Invert: true,
            EmaAlpha: 0.2,
            HysteresisPercent: 1,
            MaxBrightnessStepPercent: 2,
            Gamma: 1.0),
        Brightness: new BrightnessSettings(
            MinPercent: 10,
            MaxPercent: 100,
            Curve: CreateDefaultCurve(10, 100)),
        BaudRate: 115200,
        DiscoveryTimeoutMs: 2500,
        IsGeneric: true);

    public static readonly IReadOnlyList<DeviceProfile> All =
    [
        new DeviceProfile(
            ProfileId: "esp32c6-analog-ky018",
            DeviceId: "esp32c6-01",
            SensorId: "light0",
            MeasurementKind: MeasurementKind.Adc,
            Processing: new ProcessingSettings(
                AdcMin: 200,
                AdcMax: 3200,
                Invert: true,
                EmaAlpha: 0.2,
                HysteresisPercent: 1,
                MaxBrightnessStepPercent: 2,
                Gamma: 1.0),
            Brightness: new BrightnessSettings(
                MinPercent: 10,
                MaxPercent: 100,
                Curve: CreateDefaultCurve(10, 100)),
            BaudRate: 115200,
            DiscoveryTimeoutMs: 2500)
    ];

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
