using BrightnessSensor.DeviceReading.Models;
using Xunit;

namespace BrightnessSensor.DeviceReading.Tests;

public sealed class SensorMessageParserTests
{
    [Fact]
    public void TryParse_ReadsCalibrationFields_WhenPresent()
    {
        const string payload = """
                               {"deviceId":"esp32c6-01","sensorId":"light0","ts":123,"value":742,"raw":1840,"calibrated":true}
                               """;

        var parsed = SensorMessageParser.TryParse(payload, out var message);

        Assert.True(parsed);
        Assert.Equal("esp32c6-01", message.DeviceId);
        Assert.Equal("light0", message.SensorId);
        Assert.Equal(742, message.Value);
        Assert.Equal(1840, message.Raw);
        Assert.True(message.Calibrated);
    }
}
