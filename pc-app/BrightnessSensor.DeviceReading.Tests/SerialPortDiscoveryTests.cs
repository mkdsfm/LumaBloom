using BrightnessSensor.DeviceReading.Discovery;
using Xunit;

namespace BrightnessSensor.DeviceReading.Tests;

public sealed class SerialPortDiscoveryTests
{
    [Fact]
    public void Constructor_UsesDefaults_WhenNoParametersAreProvided()
    {
        var discovery = new SerialPortDiscovery();
        Assert.NotNull(discovery);
    }

    [Fact]
    public void Constructor_Throws_WhenBaudRateIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery(0));
    }

    [Fact]
    public void Constructor_Throws_WhenDiscoveryTimeoutIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery(discoveryTimeoutMs: 0));
    }

    [Fact]
    public void Constructor_Throws_WhenReadTimeoutIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortDiscovery(readTimeoutMs: 0));
    }

    [Fact]
    public void Constructor_Throws_WhenNewLineIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new SerialPortDiscovery(newLine: string.Empty));
    }
}
