using BrightnessSensor.DeviceReading.Models;
using Xunit;

namespace BrightnessSensor.DeviceReading.Tests;

public sealed class SensorMessageParserTests
{
    [Fact]
    public void TryParse_ReadsTelemetryWithoutCalibrationFlag()
    {
        const string payload = """
                               {"deviceId":"esp32c6-01","sensorId":"light0","ts":123,"raw":1840}
                               """;

        var parsed = SensorMessageParser.TryParse(payload, out var message);

        Assert.True(parsed);
        Assert.Equal("esp32c6-01", message.DeviceId);
        Assert.Equal("light0", message.SensorId);
        Assert.Equal(1840, message.Raw);
    }
}
