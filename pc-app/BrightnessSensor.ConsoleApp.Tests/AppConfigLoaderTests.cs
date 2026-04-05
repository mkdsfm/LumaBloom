using BrightnessSensor.ConsoleApp.Configuration;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class AppConfigLoaderTests
{
    [Fact]
    public void Load_RequiresDeviceId()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "serial": {
                                               "deviceId": "",
                                               "baudRate": 115200,
                                               "discoveryTimeoutMs": 2500
                                             },
                                             "processing": {
                                               "adcMin": 300,
                                               "adcMax": 3200,
                                               "invert": true,
                                               "emaAlpha": 0.1,
                                               "hysteresisPercent": 7,
                                               "gamma": 2
                                             },
                                             "brightness": {
                                               "minPercent": 10,
                                               "maxPercent": 100
                                             },
                                             "calibration": {
                                               "enabled": true,
                                               "sampleCount": 5,
                                               "maxReadAttempts": 20
                                             }
                                           }
                                           """);

        Assert.Contains("serial.deviceId is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsLegacyPortOnlyConfig()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "serial": {
                                               "portName": "COM8",
                                               "baudRate": 115200
                                             },
                                             "processing": {
                                               "adcMin": 300,
                                               "adcMax": 3200,
                                               "invert": true,
                                               "emaAlpha": 0.1,
                                               "hysteresisPercent": 7,
                                               "gamma": 2
                                             },
                                             "brightness": {
                                               "minPercent": 10,
                                               "maxPercent": 100
                                             },
                                             "calibration": {
                                               "enabled": true,
                                               "sampleCount": 5,
                                               "maxReadAttempts": 20
                                             }
                                           }
                                           """);

        Assert.Contains("serial.deviceId is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsNonPositiveDiscoveryTimeout()
    {
        var exception = CaptureConfigError("""
                                           {
                                             "serial": {
                                               "deviceId": "esp32c3-01",
                                               "baudRate": 115200,
                                               "discoveryTimeoutMs": 0
                                             },
                                             "processing": {
                                               "adcMin": 300,
                                               "adcMax": 3200,
                                               "invert": true,
                                               "emaAlpha": 0.1,
                                               "hysteresisPercent": 7,
                                               "gamma": 2
                                             },
                                             "brightness": {
                                               "minPercent": 10,
                                               "maxPercent": 100
                                             },
                                             "calibration": {
                                               "enabled": true,
                                               "sampleCount": 5,
                                               "maxReadAttempts": 20
                                             }
                                           }
                                           """);

        Assert.Contains("serial.discoveryTimeoutMs must be greater than 0.", exception.Message, StringComparison.Ordinal);
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
