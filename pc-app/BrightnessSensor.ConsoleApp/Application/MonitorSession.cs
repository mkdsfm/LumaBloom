using BrightnessSensor.BrightnessMath;
using BrightnessSensor.WindowsBrightness;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class MonitorSession(IMonitorBrightness monitor, BrightnessProcessor processor)
{
    public IMonitorBrightness Monitor { get; } = monitor;

    public BrightnessProcessor Processor { get; private set; } = processor;

    public bool IsEnabled { get; private set; } = true;

    public void Disable()
    {
        IsEnabled = false;
    }

    public void ReplaceProcessor(BrightnessProcessor processor)
    {
        Processor = processor;
    }
}
