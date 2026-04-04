namespace BrightnessSensor.WindowsBrightness;

public static class MonitorDiscovery
{
    public static IReadOnlyList<IMonitorBrightness> DiscoverMonitors()
    {
        var monitors = new List<IMonitorBrightness>();

        monitors.AddRange(WmiMonitor.Discover());
        monitors.AddRange(DdcMonitor.Discover());

        return monitors;
    }

    public static void LogDetectedMonitors(IReadOnlyList<IMonitorBrightness> monitors)
    {
        if (monitors.Count == 0)
        {
            Console.WriteLine("No brightness-capable monitors detected.");
            return;
        }

        var grouped = monitors.GroupBy(monitor => monitor.Source).ToList();
        foreach (var group in grouped)
        {
            Console.WriteLine($"{group.Key}: detected {group.Count()} monitor(s):");
            foreach (var monitor in group)
            {
                Console.WriteLine($"{group.Key}: - {monitor.Name}");
            }
        }
    }
}
