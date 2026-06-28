using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.DeviceReading.Models;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class AppConfigLoaderTests
{
    [Fact]
    public void Load_RejectsLegacyPortOnlyConfigWithoutProfileFallback()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "serial": {
                                               "portName": "COM8",
                                               "baudRate": 115200
                                             },
                                             "deviceProfile": {
                                               "autoDetect": false
                                             }
                                           }
                                           """);

        Assert.Contains(
            "deviceProfile.profileId is required when deviceProfile.autoDetect is false.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AllowsMissingSerialBaudRateAndDiscoveryTimeout()
    {
        var config = LoadConfig("""
                                {
                                  "deviceProfile": {
                                    "autoDetect": true
                                  }
                                }
                                """);

        Assert.Null(config.Serial.BaudRate);
        Assert.Null(config.Serial.DiscoveryTimeoutMs);
    }

    [Fact]
    public void Load_RejectsNonPositiveDiscoveryTimeout()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "serial": {
                                               "discoveryTimeoutMs": 0
                                             }
                                           }
                                           """);

        Assert.Contains("serial.discoveryTimeoutMs must be greater than 0.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_KnownProfile_ByDeviceAndSensor()
    {
        var config = LoadConfig("""
                                {
                                  "serial": {
                                    "deviceId": "esp32c3-01"
                                  }
                                }
                                """);

        var resolver = new DeviceProfileResolver();
        var message = new SensorMessage
        {
            DeviceId = "esp32c3-01",
            SensorId = "light0",
            Value = 1234
        };

        var profile = resolver.Resolve(config, message, out _);

        Assert.Equal("esp32c3-analog-ky018", profile.ProfileId);
    }

    [Fact]
    public void Resolve_UnknownProfile_FallsBackToGeneric()
    {
        var config = LoadConfig("""
                                {
                                  "serial": {}
                                }
                                """);

        var resolver = new DeviceProfileResolver();
        var message = new SensorMessage
        {
            DeviceId = "mystery-board",
            SensorId = "light9",
            Value = 1234
        };

        var profile = resolver.Resolve(config, message, out var logMessage);

        Assert.Equal(DeviceProfileCatalog.Generic.ProfileId, profile.ProfileId);
        Assert.Contains("using generic profile", logMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvedSettings_ApplyOverridesOverProfileDefaults()
    {
        var config = LoadConfig("""
                                {
                                  "serial": {
                                    "deviceId": "esp32c3-01"
                                  },
                                  "processing": {
                                    "emaAlpha": 0.1,
                                    "hysteresisPercent": 7,
                                    "maxBrightnessStepPercent": 4
                                  },
                                  "brightness": {
                                    "minPercent": 15
                                  },
                                  "calibration": {
                                    "enabled": false,
                                    "sampleCount": 3,
                                    "maxReadAttempts": 3
                                  }
                                }
                                """);

        var resolved = ResolvedSettingsFactory.Create(
            config,
            DeviceProfileCatalog.All.Single(profile => profile.ProfileId == "esp32c3-analog-ky018"));

        Assert.Equal(300, resolved.Processing.AdcMin);
        Assert.Equal(3200, resolved.Processing.AdcMax);
        Assert.Equal(0.1, resolved.Processing.EmaAlpha);
        Assert.Equal(7, resolved.Processing.HysteresisPercent);
        Assert.Equal(4, resolved.Processing.MaxBrightnessStepPercent);
        Assert.Equal(15, resolved.Brightness.MinPercent);
        Assert.Equal(100, resolved.Brightness.MaxPercent);
        Assert.False(resolved.Calibration.Enabled);
        Assert.Equal(3, resolved.Calibration.SampleCount);
        Assert.Equal(3, resolved.Calibration.MaxReadAttempts);
        Assert.Equal(SerialSettings.DefaultBaudRate, resolved.BaudRate);
        Assert.Equal(SerialSettings.DefaultDiscoveryTimeoutMs, resolved.DiscoveryTimeoutMs);
    }

    [Fact]
    public void Resolve_Esp32C6Profile_UsesNormalized1000Measurement()
    {
        var config = LoadConfig("""
                                {
                                  "serial": {
                                    "deviceId": "esp32c6-01"
                                  }
                                }
                                """);

        var resolver = new DeviceProfileResolver();
        var message = new SensorMessage
        {
            DeviceId = "esp32c6-01",
            SensorId = "light0",
            Value = 0,
            Raw = 1450,
            Calibrated = false
        };

        var profile = resolver.Resolve(config, message, out _);
        var resolved = ResolvedSettingsFactory.Create(config, profile);

        Assert.Equal(MeasurementKind.Normalized1000, resolved.MeasurementKind);
        Assert.False(resolved.Processing.Invert);
        Assert.Equal(1000, resolved.Processing.AdcMax);
    }

    private static AppConfig LoadConfig(string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-tests-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json);

        try
        {
            return AppConfigLoader.Load(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static InvalidOperationException CaptureConfigError(string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-tests-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json);

        try
        {
            return Assert.Throws<InvalidOperationException>(() => AppConfigLoader.Load(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
