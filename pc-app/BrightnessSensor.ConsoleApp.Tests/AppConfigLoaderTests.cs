using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
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
    public void EnsureDefaultFile_CreatesLoadableConfig()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-default-{Guid.NewGuid():N}.json");

        try
        {
            AppConfigLoader.EnsureDefaultFile(tempPath);
            var config = AppConfigLoader.Load(tempPath);

            Assert.True(config.DeviceProfile.AutoDetect);
            Assert.Equal("en", config.Ui.Language);
            Assert.Equal(10, config.Brightness!.MinPercent);
            Assert.Equal(100, config.Brightness.MaxPercent);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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
    public void Load_AllowsSupportedUiLanguage()
    {
        var config = LoadConfig("""
                                {
                                  "ui": {
                                    "language": "ru"
                                  }
                                }
                                """);

        Assert.Equal("ru", config.Ui.Language);
    }

    [Fact]
    public void Load_RejectsUnsupportedUiLanguage()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "ui": {
                                               "language": "fr"
                                             }
                                           }
                                           """);

        Assert.Contains("ui.language must be one of: auto, en, ru, es.", exception.Message, StringComparison.Ordinal);
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
    public void ResolvedSettings_IgnoresLegacyIncompleteBrightnessCurve()
    {
        var config = LoadConfig("""
                                {
                                  "brightness": {
                                    "minPercent": 10,
                                    "maxPercent": 100,
                                    "curve": [
                                      { "lightPercent": 0, "brightnessPercent": 10 }
                                    ]
                                  }
                                }
                                """);

        var resolved = ResolvedSettingsFactory.Create(config, DeviceProfileCatalog.Generic);

        Assert.Equal([0, 25, 50, 75, 100], resolved.Brightness.Curve.Select(point => point.LightPercent).ToArray());
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

    [Fact]
    public void Writer_UpdatesUiLanguageOnly()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "serial": {
                                            "deviceId": "esp32c6-01"
                                          },
                                          "ui": {
                                            "language": "en"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateUiLanguage(tempPath, "es");
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal("esp32c6-01", config.Serial.DeviceId);
            Assert.Equal("es", config.Ui.Language);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_UpdatesProcessingValueOnly()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "processing": {
                                            "adcMin": 300,
                                            "adcMax": 3200,
                                            "emaAlpha": 0.25
                                          },
                                          "ui": {
                                            "language": "ru"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateProcessing(tempPath, ProcessingParameter.EmaAlpha, "0.5");
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal("ru", config.Ui.Language);
            Assert.Equal(300, config.Processing!.AdcMin);
            Assert.Equal(3200, config.Processing.AdcMax);
            Assert.Equal(0.5, config.Processing.EmaAlpha);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_UpdatesBrightnessCurvePointOnly()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "brightness": {
                                            "minPercent": 10,
                                            "maxPercent": 100,
                                            "curve": [
                                              { "lightPercent": 0, "brightnessPercent": 10 },
                                              { "lightPercent": 50, "brightnessPercent": 55 },
                                              { "lightPercent": 100, "brightnessPercent": 100 }
                                            ]
                                          },
                                          "ui": {
                                            "language": "ru"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateBrightnessCurvePoint(tempPath, 50, 42);
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal("ru", config.Ui.Language);
            Assert.Equal(10, config.Brightness!.MinPercent);
            Assert.Equal(100, config.Brightness.MaxPercent);
            Assert.Contains(config.Brightness.Curve!, point => point.LightPercent == 50 && point.BrightnessPercent == 42);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_CreatesFullBrightnessCurveWhenMissing()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "brightness": {
                                            "minPercent": 10,
                                            "maxPercent": 100
                                          },
                                          "ui": {
                                            "language": "ru"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateBrightnessCurvePoint(tempPath, 50, 42);
            var config = AppConfigLoader.Load(tempPath);
            var curve = config.Brightness!.Curve!;

            Assert.Equal([0, 25, 50, 75, 100], curve.Select(point => point.LightPercent).ToArray());
            Assert.Contains(curve, point => point.LightPercent == 50 && point.BrightnessPercent == 42);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_PersistedSettingsAreUsedAfterReload()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "serial": {
                                            "deviceId": "esp32c6-01"
                                          },
                                          "brightness": {
                                            "minPercent": 10,
                                            "maxPercent": 100
                                          },
                                          "ui": {
                                            "language": "en"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateUiLanguage(tempPath, "es");
            AppConfigWriter.UpdateProcessing(tempPath, ProcessingParameter.EmaAlpha, "0.5");
            AppConfigWriter.UpdateProcessing(tempPath, ProcessingParameter.MaxBrightnessStepPercent, "7");
            AppConfigWriter.UpdateBrightnessCurvePoint(tempPath, 75, 66);

            var reloaded = AppConfigLoader.Load(tempPath);
            var resolved = ResolvedSettingsFactory.Create(reloaded, DeviceProfileCatalog.Generic);

            Assert.Equal("es", reloaded.Ui.Language);
            Assert.Equal(0.5, resolved.Processing.EmaAlpha);
            Assert.Equal(7, resolved.Processing.MaxBrightnessStepPercent);
            Assert.Contains(resolved.Brightness.Curve, point => point.LightPercent == 75 && point.BrightnessPercent == 66);
        }
        finally
        {
            File.Delete(tempPath);
        }
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

    private static string CreateTempConfig(string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-tests-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json);
        return tempPath;
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
