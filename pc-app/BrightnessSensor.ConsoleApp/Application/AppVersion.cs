using System.Reflection;

namespace BrightnessSensor.ConsoleApp.Application;

internal static class AppVersion
{
    public static string Current => ParseCurrentVersion();

    private static string ParseCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var clean = informational.Split('+', 2, StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
