using System.Management;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class WmiMonitor(string instanceName) : IMonitorBrightness
{
    public string Source => "WMI";

    public string Name => instanceName;

    public bool TryGetBrightness(out int brightnessPercent, out string? error)
    {
        brightnessPercent = 0;
        error = null;

        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            using var monitorClass = new ManagementClass(scope, new ManagementPath("WmiMonitorBrightness"), null);
            using var instances = monitorClass.GetInstances();

            foreach (var o in instances)
            {
                var monitor = (ManagementObject) o;
                using (monitor)
                {
                    if (monitor["InstanceName"] is not string name ||
                        !string.Equals(name, instanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (monitor["CurrentBrightness"] is byte current)
                    {
                        brightnessPercent = current;
                        return true;
                    }
                }
            }

            error = "Brightness value not found for WMI monitor.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetBrightness(int brightnessPercent, out string? error)
    {
        error = null;

        if (brightnessPercent is < 0 or > 100)
        {
            error = "brightnessPercent must be in range 0..100";
            return false;
        }

        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            using var monitorClass = new ManagementClass(scope, new ManagementPath("WmiMonitorBrightnessMethods"), null);
            using var instances = monitorClass.GetInstances();

            foreach (var o in instances)
            {
                var monitor = (ManagementObject) o;
                using (monitor)
                {
                    if (monitor["InstanceName"] is not string name ||
                        !string.Equals(name, instanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // WmiSetBrightness(uint timeout, byte brightness)
                    monitor.InvokeMethod("WmiSetBrightness", [(uint)0, (byte)brightnessPercent]);
                    return true;
                }
            }

            error = "Brightness method not found for WMI monitor.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<IMonitorBrightness> Discover()
    {
        var list = new List<IMonitorBrightness>();

        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            using var monitorClass = new ManagementClass(scope, new ManagementPath("WmiMonitorBrightness"), null);
            using var instances = monitorClass.GetInstances();

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in instances)
            {
                var monitor = (ManagementObject) o;
                using (monitor)
                {
                    if (monitor["InstanceName"] is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name.Trim());
                    }
                }
            }

            foreach (var name in names)
            {
                list.Add(new WmiMonitor(name));
            }
        }
        catch
        {
            // ignore discovery errors
        }

        return list;
    }
}
