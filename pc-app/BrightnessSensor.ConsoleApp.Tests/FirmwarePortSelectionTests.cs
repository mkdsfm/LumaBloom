using BrightnessSensor.ConsoleApp.Application;
using BrightnessSensor.ConsoleApp.Runtime;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class FirmwarePortSelectionTests
{
    [Fact]
    public void Catalog_SortsAndDeduplicatesPorts()
    {
        var catalog = new SerialPortCatalog(() => ["COM12", "com3", "COM3", "COM8"]);

        var result = catalog.GetPorts();

        Assert.True(result.IsSuccess);
        Assert.Equal(["COM12", "com3", "COM8"], result.PortNames);
    }

    [Fact]
    public void Catalog_ReturnsSafeError_WhenEnumerationFails()
    {
        var catalog = new SerialPortCatalog(() => throw new UnauthorizedAccessException("denied"));

        var result = catalog.GetPorts();

        Assert.False(result.IsSuccess);
        Assert.Empty(result.PortNames);
        Assert.Contains("denied", result.Error);
    }

    [Fact]
    public void Catalog_IncludesFriendlyNameAndEspressifMarker()
    {
        var info = new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM9"] = new SerialPortInfo("COM9", "USB Serial Device", IsEspressifDevice: true)
        };
        var catalog = new SerialPortCatalog(() => ["COM9"], () => info);

        var result = catalog.GetPorts();

        var port = Assert.Single(result.Ports);
        Assert.Equal("USB Serial Device", port.Description);
        Assert.True(port.IsEspressifDevice);
    }

    [Fact]
    public void ConnectionPort_IsDefaultUntilManualPortIsSelected()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwarePorts(["COM3", "COM8"]);
        state.SetConnection("COM8", 115200, "Connected");

        Assert.Equal("COM8", state.GetSnapshot().SelectedFirmwarePort);
        Assert.False(state.GetSnapshot().IsFirmwarePortManuallySelected);

        Assert.True(state.SelectFirmwarePort("COM3"));
        state.SetFirmwarePorts(["COM8", "COM3"]);

        Assert.Equal("COM3", state.GetSnapshot().SelectedFirmwarePort);
        Assert.True(state.GetSnapshot().IsFirmwarePortManuallySelected);
    }

    [Fact]
    public void MissingManualPort_FallsBackToAutomaticPort()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwarePorts(["COM3", "COM8"]);
        state.SetConnection("COM8", 115200, "Connected");
        state.SelectFirmwarePort("COM3");

        state.SetFirmwarePorts(["COM8", "COM12"]);

        Assert.Equal("COM8", state.GetSnapshot().SelectedFirmwarePort);
        Assert.False(state.GetSnapshot().IsFirmwarePortManuallySelected);
    }

    [Fact]
    public void FlashRequest_CapturesSelectedPortAndRejectsDuplicatePendingRequest()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwarePorts(["COM3", "COM8"]);
        state.SelectFirmwarePort("COM3");

        Assert.True(state.RequestBundledFirmwareFlash());
        Assert.False(state.RequestBundledFirmwareFlash());
        state.SelectFirmwarePort("COM8");

        Assert.True(state.TryConsumeFirmwareUpdateRequest(out var request));
        Assert.Equal("COM3", request.PortName);
        Assert.Equal("firmware.bin", request.FirmwareFileName);
        Assert.False(state.TryConsumeFirmwareUpdateRequest(out _));
    }

    [Fact]
    public void FlashRequest_CapturesManuallySelectedFirmwareVersion()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwareVersionOptions([
            new FirmwareVersionOption("2.1.0", "firmware-2.1.0.bin"),
            new FirmwareVersionOption("2.0.0", "firmware-2.0.0.bin")
        ]);
        state.SetFirmwarePorts(["COM8"]);

        Assert.True(state.SelectFirmwareVersion("firmware-2.0.0.bin"));
        Assert.True(state.SelectFirmwarePort("COM8"));
        Assert.True(state.RequestBundledFirmwareFlash());
        Assert.True(state.TryConsumeFirmwareUpdateRequest(out var request));
        Assert.Equal("firmware-2.0.0.bin", request.FirmwareFileName);
        Assert.Equal("2.0.0", state.GetSnapshot().BundledFirmware!.Version);
    }

    [Fact]
    public void Coordinator_FlashesRequestedPortAndRunsReleaseCallback()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwarePorts(["COM8"]);
        var flashService = new FakeFirmwareFlashService();
        var coordinator = new FirmwareUpdateCoordinator(new SerialPortCatalog(() => ["COM8"]), flashService);
        var released = false;

        var attempted = coordinator.Execute(
            new FirmwareUpdateActionRequest("COM8", "firmware.bin"),
            CreateFirmwareInfo(),
            state,
            () => released = true);

        Assert.True(attempted);
        Assert.True(released);
        Assert.Equal("COM8", flashService.PortName);
        Assert.Equal("firmware.bin", flashService.FirmwareFileName);
        Assert.Contains("COM8", state.GetSnapshot().BundledFirmware!.StatusMessage);
    }

    [Fact]
    public void Coordinator_DoesNotFlashPortThatDisappeared()
    {
        var state = CreateStateWithFirmware();
        state.SetFirmwarePorts(["COM8"]);
        var flashService = new FakeFirmwareFlashService();
        var coordinator = new FirmwareUpdateCoordinator(new SerialPortCatalog(() => ["COM12"]), flashService);

        var attempted = coordinator.Execute(new FirmwareUpdateActionRequest("COM8", "firmware.bin"), CreateFirmwareInfo(), state);

        Assert.False(attempted);
        Assert.Null(flashService.PortName);
        Assert.Contains("COM8", state.GetSnapshot().BundledFirmware!.StatusMessage);
    }

    [Fact]
    public void FlashStartInfo_UsesSeparateArgumentsForSelectedPortAndFirmwarePath()
    {
        var firmware = CreateFirmwareInfo(@"C:\Program Files\LumaBloom\Firmware\firmware merged.bin");

        var startInfo = FirmwareFlashService.CreateStartInfo(
            @"C:\Program Files\LumaBloom\Tools\esptool.exe",
            @"C:\Program Files\LumaBloom",
            firmware,
            "COM12");

        Assert.Equal(
            ["--chip", "esp32c6", "--port", "COM12", "--baud", "460800", "write-flash", "0x0", firmware.AbsolutePath],
            startInfo.ArgumentList);
    }

    private static RuntimeStateStore CreateStateWithFirmware()
    {
        var state = new RuntimeStateStore();
        state.SetFirmwareVersionOptions([new FirmwareVersionOption("2.1.0", "firmware.bin")]);
        state.SetBundledFirmwareState(new BundledFirmwareSnapshot("2.1.0", "firmware.bin", "Ready", IsAvailable: true, IsBusy: false));
        return state;
    }

    private static BundledFirmwareInfo CreateFirmwareInfo(string path = @"C:\LumaBloom\Firmware\firmware.bin")
    {
        return new BundledFirmwareInfo("2.1.0", "esptool", "esp32c6", 460800, "0x0", Path.GetFileName(path), path);
    }

    private sealed class FakeFirmwareFlashService : IFirmwareFlashService
    {
        public string? PortName { get; private set; }
        public string? FirmwareFileName { get; private set; }

        public void Flash(BundledFirmwareInfo firmwareInfo, string portName)
        {
            PortName = portName;
            FirmwareFileName = firmwareInfo.FileName;
        }
    }
}
