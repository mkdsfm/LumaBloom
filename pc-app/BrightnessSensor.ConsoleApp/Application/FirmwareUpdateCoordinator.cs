using BrightnessSensor.ConsoleApp.Runtime;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class FirmwareUpdateCoordinator(SerialPortCatalog portCatalog, IFirmwareFlashService firmwareFlashService)
{
    private readonly SerialPortCatalog _portCatalog = portCatalog;
    private readonly IFirmwareFlashService _firmwareFlashService = firmwareFlashService;

    public bool Execute(FirmwareUpdateActionRequest request, BundledFirmwareInfo? bundledFirmware, RuntimeStateStore stateStore, Action? beforeFlash = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stateStore);

        var currentFirmwareState = stateStore.GetSnapshot().BundledFirmware ??
                                   new BundledFirmwareSnapshot("Unknown", "n/a", "Bundled firmware state unavailable.", false, false);
        if (bundledFirmware is null)
        {
            const string missingStatus = "Bundled firmware is not available in this application package.";
            stateStore.SetBundledFirmwareState(currentFirmwareState with { StatusMessage = missingStatus, IsBusy = false });
            stateStore.AddEvent(missingStatus, RuntimeEventSeverity.Warning);
            return false;
        }

        var ports = _portCatalog.GetPorts();
        if (!ports.IsSuccess)
        {
            stateStore.SetFirmwarePortListError(ports.Error!);
            stateStore.SetBundledFirmwareState(currentFirmwareState with { StatusMessage = ports.Error!, IsBusy = false });
            stateStore.AddEvent(ports.Error!, RuntimeEventSeverity.Error);
            return false;
        }

        stateStore.SetFirmwarePorts(ports.Ports);
        if (!ports.PortNames.Any(port => string.Equals(port, request.PortName, StringComparison.OrdinalIgnoreCase)))
        {
            var missingPortStatus = $"Selected COM port '{request.PortName}' is no longer available. Select a port and try again.";
            stateStore.SetBundledFirmwareState(currentFirmwareState with { StatusMessage = missingPortStatus, IsBusy = false });
            stateStore.AddEvent(missingPortStatus, RuntimeEventSeverity.Warning);
            return false;
        }

        stateStore.SetBundledFirmwareState(currentFirmwareState with
        {
            StatusMessage = $"Flashing bundled firmware {bundledFirmware.Version} on {request.PortName}...",
            IsBusy = true
        });

        try
        {
            beforeFlash?.Invoke();
            _firmwareFlashService.Flash(bundledFirmware, request.PortName);
            stateStore.SetBundledFirmwareState(currentFirmwareState with
            {
                Version = bundledFirmware.Version,
                FileName = bundledFirmware.FileName,
                StatusMessage = $"Bundled firmware {bundledFirmware.Version} flashed successfully on {request.PortName}.",
                IsAvailable = true,
                IsBusy = false
            });
            stateStore.AddEvent(
                $"Bundled firmware {bundledFirmware.Version} flashed successfully on {request.PortName}.",
                RuntimeEventSeverity.Success);
        }
        catch (Exception exception)
        {
            stateStore.SetBundledFirmwareState(currentFirmwareState with
            {
                Version = bundledFirmware.Version,
                FileName = bundledFirmware.FileName,
                StatusMessage = $"Firmware flashing failed on {request.PortName}: {exception.Message}",
                IsAvailable = true,
                IsBusy = false
            });
            stateStore.AddEvent($"Firmware flashing failed on {request.PortName}: {exception.Message}", RuntimeEventSeverity.Error);
        }

        return true;
    }
}
