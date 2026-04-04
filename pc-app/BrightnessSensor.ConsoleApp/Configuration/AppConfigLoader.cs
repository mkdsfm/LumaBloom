using System.Text.Json;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Loads appsettings.json and validates required ranges/fields.
internal static class AppConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ResolveDefaultPath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return File.Exists(outputPath)
            ? outputPath
            : Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    }

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Config file not found: {path}");
        }

        var rawJson = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AppConfig>(rawJson, JsonOptions) ??
            throw new InvalidOperationException("Failed to parse appsettings.json.");

        Validate(config);
        return config;
    }

    private static void Validate(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Serial.PortName))
        {
            throw new InvalidOperationException("serial.portName is required.");
        }

        if (config.Serial.BaudRate <= 0)
        {
            throw new InvalidOperationException("serial.baudRate must be greater than 0.");
        }

        if (config.Processing.AdcMax <= config.Processing.AdcMin)
        {
            throw new InvalidOperationException("processing.adcMax must be greater than processing.adcMin.");
        }

        if (config.Processing.EmaAlpha is <= 0 or > 1)
        {
            throw new InvalidOperationException("processing.emaAlpha must be in the range (0, 1].");
        }

        if (config.Processing.HysteresisPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("processing.hysteresisPercent must be in the range 0..100.");
        }

        if (config.Processing.Gamma is <= 0)
        {
            throw new InvalidOperationException("processing.gamma must be greater than 0 when specified.");
        }

        if (config.Brightness.MinPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.minPercent must be in the range 0..100.");
        }

        if (config.Brightness.MaxPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.maxPercent must be in the range 0..100.");
        }

        if (config.Brightness.MinPercent > config.Brightness.MaxPercent)
        {
            throw new InvalidOperationException(
                "brightness.minPercent cannot be greater than brightness.maxPercent.");
        }

        if (config.Calibration.SampleCount <= 0)
        {
            throw new InvalidOperationException("calibration.sampleCount must be greater than 0.");
        }

        if (config.Calibration.MaxReadAttempts <= 0)
        {
            throw new InvalidOperationException("calibration.maxReadAttempts must be greater than 0.");
        }

        if (config.Calibration.MaxReadAttempts < config.Calibration.SampleCount)
        {
            throw new InvalidOperationException(
                "calibration.maxReadAttempts must be greater than or equal to calibration.sampleCount.");
        }
    }
}
