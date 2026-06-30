using BrightnessSensor.BrightnessMath;
using BrightnessSensor.WindowsBrightness;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class MonitorSession(IMonitorBrightness monitor, BrightnessProcessor processor)
{
    public IMonitorBrightness Monitor { get; } = monitor;

    public BrightnessProcessor Processor { get; } = processor;

    public bool IsEnabled { get; private set; } = true;

    public void Disable()
    {
        IsEnabled = false;
    }
}
