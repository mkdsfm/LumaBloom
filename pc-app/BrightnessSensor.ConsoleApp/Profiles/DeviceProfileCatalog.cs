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
            HysteresisPercent: 3,
            Gamma: 1.0),
        Calibration: new CalibrationSettings(
            Enabled: true,
            SampleCount: 5,
            MaxReadAttempts: 20),
        Brightness: new BrightnessSettings(
            MinPercent: 10,
            MaxPercent: 100),
        BaudRate: SerialSettings.DefaultBaudRate,
        DiscoveryTimeoutMs: SerialSettings.DefaultDiscoveryTimeoutMs,
        IsGeneric: true);

    public static readonly IReadOnlyList<DeviceProfile> All =
    [
        new DeviceProfile(
            ProfileId: "esp32c3-analog-ky018",
            DeviceId: "esp32c3-01",
            SensorId: "light0",
            MeasurementKind: MeasurementKind.Adc,
            Processing: new ProcessingSettings(
                AdcMin: 300,
                AdcMax: 3200,
                Invert: true,
                EmaAlpha: 0.2,
                HysteresisPercent: 3,
                Gamma: 2.0),
            Calibration: new CalibrationSettings(
                Enabled: true,
                SampleCount: 5,
                MaxReadAttempts: 20),
            Brightness: new BrightnessSettings(
                MinPercent: 10,
                MaxPercent: 100),
            BaudRate: SerialSettings.DefaultBaudRate,
            DiscoveryTimeoutMs: SerialSettings.DefaultDiscoveryTimeoutMs),
        new DeviceProfile(
            ProfileId: "esp32c6-analog-ky018",
            DeviceId: "esp32c6-01",
            SensorId: "light0",
            MeasurementKind: MeasurementKind.Adc,
            Processing: new ProcessingSettings(
                AdcMin: 0,
                AdcMax: 4095,
                Invert: true,
                EmaAlpha: 0.2,
                HysteresisPercent: 3,
                Gamma: 1.0),
            Calibration: new CalibrationSettings(
                Enabled: true,
                SampleCount: 5,
                MaxReadAttempts: 20),
            Brightness: new BrightnessSettings(
                MinPercent: 10,
                MaxPercent: 100),
            BaudRate: SerialSettings.DefaultBaudRate,
            DiscoveryTimeoutMs: SerialSettings.DefaultDiscoveryTimeoutMs)
    ];
}
