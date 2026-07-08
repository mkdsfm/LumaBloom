using BrightnessSensor.DeviceReading.Models;
using Xunit;

namespace BrightnessSensor.DeviceReading.Tests;

public sealed class SensorMessageParserTests
{
    [Fact]
    public void TryParse_AcceptsValidLumaBloomTelemetry()
    {
        const string payload = """
                               {"id":"lumabloom","ts":123,"raw":1840}
                               """;

        var parsed = SensorMessageParser.TryParse(payload, out var message);

        Assert.True(parsed);
        Assert.Equal("lumabloom", message.Id);
        Assert.Equal(123, message.Timestamp);
        Assert.Equal(1840, message.Raw);
    }

    [Theory]
    [InlineData("""{"ts":123,"raw":1840}""")]
    [InlineData("""{"id":"other","ts":123,"raw":1840}""")]
    [InlineData("""{"id":"lumabloom","raw":1840}""")]
    [InlineData("""{"id":"lumabloom","ts":123}""")]
    public void TryParse_RejectsUnsupportedOrIncompleteTelemetry(string payload)
    {
        var parsed = SensorMessageParser.TryParse(payload, out _);

        Assert.False(parsed);
    }
}
