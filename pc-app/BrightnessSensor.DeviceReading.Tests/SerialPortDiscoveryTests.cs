using BrightnessSensor.DeviceReading.Discovery;
using Xunit;

namespace BrightnessSensor.DeviceReading.Tests;

public sealed class SerialPortDiscoveryTests
{
    [Fact]
    public void Constructor_AllowsEmptyDeviceId()
    {
        var discovery = new SerialPortDiscovery(string.Empty);
        Assert.NotNull(discovery);
    }

    [Fact]
    public void Constructor_Throws_WhenBaudRateIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery("esp32c3-01", 0));
    }

    [Fact]
    public void Constructor_Throws_WhenDiscoveryTimeoutIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery("esp32c3-01", discoveryTimeoutMs: 0));
    }

    [Fact]
    public void Constructor_Throws_WhenReadTimeoutIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery("esp32c3-01", readTimeoutMs: 0));
    }

    [Fact]
    public void Constructor_Throws_WhenNewLineIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new SerialPortDiscovery("esp32c3-01", newLine: string.Empty));
    }
}
