using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class AppConfigLoaderTests
{
    [Fact]
    public void Load_IgnoresLegacySerialAndDeviceProfileSections()
    {
        var config = LoadConfig("""
                                {
                                  "serial": {
                                    "portName": "COM8",
                                    "baudRate": 115200
                                  },
                                  "deviceProfile": {
                                    "autoDetect": false,
                                    "profileId": "esp32c6-analog-ky018"
                                  }
                                }
                                """);

        Assert.Null(config.Connection);
        Assert.Null(config.Processing);
        Assert.Null(config.Brightness);
    }

    [Fact]
    public void Load_AllowsMinimalConfig()
    {
        var config = LoadConfig("""
                                {
                                  "ui": {
                                    "language": "auto"
                                  }
                                }
                                """);

        Assert.Equal("auto", config.Ui.Language);
        Assert.Null(config.Connection);
    }

    [Fact]
    public void EnsureDefaultFile_CreatesLoadableConfig()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-default-{Guid.NewGuid():N}.json");

        try
        {
            AppConfigLoader.EnsureDefaultFile(tempPath);
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal(115200, config.Connection!.BaudRate);
            Assert.Equal(2500, config.Connection.DiscoveryTimeoutMs);
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
    public void Load_RejectsInvalidConnectionValues()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "connection": {
                                               "baudRate": 0
                                             }
                                           }
                                           """);

        Assert.Contains("connection.baudRate must be greater than zero.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedSettings_ApplyOverridesOverBuiltInDefaults()
    {
        var config = LoadConfig("""
                                {
                                  "connection": {
                                    "baudRate": 230400,
                                    "discoveryTimeoutMs": 4000
                                  },
                                  "processing": {
                                    "emaAlpha": 0.1,
                                    "hysteresisPercent": 7,
                                    "maxBrightnessStepPercent": 4
                                  },
                                  "brightness": {
                                    "minPercent": 15
                                  }
                                }
                                """);

        var resolved = ResolvedSettingsFactory.Create(config);

        Assert.Equal("lumabloom", resolved.ProtocolId);
        Assert.Equal(MeasurementKind.Adc, resolved.MeasurementKind);
        Assert.Equal(200, resolved.Processing.AdcMin);
        Assert.Equal(3200, resolved.Processing.AdcMax);
        Assert.Equal(0.1, resolved.Processing.EmaAlpha);
        Assert.Equal(7, resolved.Processing.HysteresisPercent);
        Assert.Equal(4, resolved.Processing.MaxBrightnessStepPercent);
        Assert.Equal(15, resolved.Brightness.MinPercent);
        Assert.Equal(100, resolved.Brightness.MaxPercent);
        Assert.Equal(230400, resolved.BaudRate);
        Assert.Equal(4000, resolved.DiscoveryTimeoutMs);
    }

    [Fact]
    public void ResolvedSettings_UsesDefaultCurve_WhenConfiguredCurveIsIncomplete()
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

        var resolved = ResolvedSettingsFactory.Create(config);

        Assert.Equal([0, 25, 50, 75, 100], resolved.Brightness.Curve.Select(point => point.LightPercent).ToArray());
    }

    [Fact]
    public void Writer_UpdatesUiLanguageOnly()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "ui": {
                                            "language": "en"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateUiLanguage(tempPath, "es");
            var config = AppConfigLoader.Load(tempPath);

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
                                              { "lightPercent": 25, "brightnessPercent": 30 },
                                              { "lightPercent": 50, "brightnessPercent": 55 },
                                              { "lightPercent": 75, "brightnessPercent": 75 },
                                              { "lightPercent": 100, "brightnessPercent": 100 }
                                            ]
                                          },
                                          "ui": {
                                            "language": "en"
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateBrightnessCurvePoint(tempPath, 50, 61);
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal("en", config.Ui.Language);
            Assert.Equal(61, config.Brightness!.Curve!.Single(point => point.LightPercent == 50).BrightnessPercent);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_SeedsDefaultCurve_WhenExistingCurveIsMissing()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "brightness": {
                                            "minPercent": 20,
                                            "maxPercent": 80
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateBrightnessCurvePoint(tempPath, 50, 60);
            var config = AppConfigLoader.Load(tempPath);

            Assert.Equal([0, 25, 50, 75, 100], config.Brightness!.Curve!.Select(point => point.LightPercent).ToArray());
            Assert.Equal(60, config.Brightness!.Curve!.Single(point => point.LightPercent == 50).BrightnessPercent);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Writer_RewritesCurve_WhenAnchorAddsIntermediatePoint()
    {
        var tempPath = CreateTempConfig("""
                                        {
                                          "brightness": {
                                            "curve": [
                                              { "lightPercent": 0, "brightnessPercent": 10 },
                                              { "lightPercent": 25, "brightnessPercent": 30 },
                                              { "lightPercent": 50, "brightnessPercent": 55 },
                                              { "lightPercent": 75, "brightnessPercent": 75 },
                                              { "lightPercent": 100, "brightnessPercent": 100 }
                                            ]
                                          }
                                        }
                                        """);

        try
        {
            AppConfigWriter.UpdateBrightnessCurve(tempPath,
            [
                new BrightnessCurvePoint(0, 10),
                new BrightnessCurvePoint(16, 60),
                new BrightnessCurvePoint(25, 65),
                new BrightnessCurvePoint(50, 75),
                new BrightnessCurvePoint(100, 100)
            ]);

            var reloaded = AppConfigLoader.Load(tempPath);
            var resolved = ResolvedSettingsFactory.Create(reloaded);

            Assert.Equal([0, 16, 25, 50, 100], resolved.Brightness.Curve.Select(point => point.LightPercent).ToArray());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static AppConfig LoadConfig(string json)
    {
        var tempPath = CreateTempConfig(json);

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
        var tempPath = CreateTempConfig(json);

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

    private static string CreateTempConfig(string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brightness-sensor-tests-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json);
        return tempPath;
    }
}
