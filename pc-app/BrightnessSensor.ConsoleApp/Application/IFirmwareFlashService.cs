namespace BrightnessSensor.ConsoleApp.Application;

internal interface IFirmwareFlashService
{
    void Flash(BundledFirmwareInfo firmwareInfo, string portName);
}
