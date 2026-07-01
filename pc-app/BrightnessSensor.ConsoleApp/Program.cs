using System.Text;
using BrightnessSensor.ConsoleApp.Application;
using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp;

// Entry point: initializes console encoding and starts the Windows app loop.
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("This application supports Windows only.");
            return 1;
        }

        var loadResult = TryLoadConfig(args, out var config, out var configPath, out var configError);
        if (!loadResult)
        {
            Console.Error.WriteLine($"Configuration error: {configError}");
            return 1;
        }
        if (config == null)
        {
            Console.Error.WriteLine("No configuration found.");
            return 1;
        }
        
        return BrightnessApplication.Run(config, configPath);
    }
    
    private static bool TryLoadConfig(string[] args, out AppConfig? config, out string configPath, out string error)
    {
        try
        {
            configPath = args.Length > 0 ? args[0] : AppConfigLoader.ResolveDefaultPath();
            if (args.Length == 0)
            {
                AppConfigLoader.EnsureDefaultFile(configPath);
            }

            config = AppConfigLoader.Load(configPath);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            config = null;
            configPath = string.Empty;
            error = exception.Message;
            return false;
        }
    }
}
