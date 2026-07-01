using System.Text.Json;
using System.Globalization;

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
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public static void EnsureDefaultFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, """
                                {
                                  "deviceProfile": {
                                    "autoDetect": true
                                  },
                                  "processing": {
                                    "emaAlpha": 0.25,
                                    "hysteresisPercent": 1,
                                    "maxBrightnessStepPercent": 2
                                  },
                                  "brightness": {
                                    "minPercent": 10,
                                    "maxPercent": 100
                                  },
                                  "ui": {
                                    "language": "en"
                                  }
                                }
                                """);
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
        if (config.Serial.BaudRate is <= 0)
        {
            throw new InvalidOperationException("serial.baudRate must be greater than 0.");
        }

        if (config.Serial.DiscoveryTimeoutMs is <= 0)
        {
            throw new InvalidOperationException("serial.discoveryTimeoutMs must be greater than 0.");
        }

        if (!config.DeviceProfile.AutoDetect &&
            string.IsNullOrWhiteSpace(config.DeviceProfile.ProfileId))
        {
            throw new InvalidOperationException(
                "deviceProfile.profileId is required when deviceProfile.autoDetect is false.");
        }

        if (config.Processing?.AdcMin.HasValue == true &&
            config.Processing.AdcMax.HasValue &&
            config.Processing.AdcMax.Value <= config.Processing.AdcMin.Value)
        {
            throw new InvalidOperationException("processing.adcMax must be greater than processing.adcMin.");
        }

        if (config.Processing?.EmaAlpha is <= 0 or > 1)
        {
            throw new InvalidOperationException("processing.emaAlpha must be in the range (0, 1].");
        }

        if (config.Processing?.HysteresisPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("processing.hysteresisPercent must be in the range 0..100.");
        }

        if (config.Processing?.MaxBrightnessStepPercent is <= 0 or > 100)
        {
            throw new InvalidOperationException("processing.maxBrightnessStepPercent must be in the range 1..100.");
        }

        if (config.Processing?.Gamma is <= 0)
        {
            throw new InvalidOperationException("processing.gamma must be greater than 0 when specified.");
        }

        if (config.Brightness?.MinPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.minPercent must be in the range 0..100.");
        }

        if (config.Brightness?.MaxPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("brightness.maxPercent must be in the range 0..100.");
        }

        if (config.Brightness?.MinPercent.HasValue == true &&
            config.Brightness.MaxPercent.HasValue &&
            config.Brightness.MinPercent.Value > config.Brightness.MaxPercent.Value)
        {
            throw new InvalidOperationException(
                "brightness.minPercent cannot be greater than brightness.maxPercent.");
        }

        if (config.Brightness?.Curve is { Count: >= 2 })
        {
            ValidateBrightnessCurve(config.Brightness.Curve);
        }

        if (config.Calibration?.SampleCount is <= 0)
        {
            throw new InvalidOperationException("calibration.sampleCount must be greater than 0.");
        }

        if (config.Calibration?.MaxReadAttempts is <= 0)
        {
            throw new InvalidOperationException("calibration.maxReadAttempts must be greater than 0.");
        }

        if (config.Calibration?.SampleCount.HasValue == true &&
            config.Calibration.MaxReadAttempts.HasValue &&
            config.Calibration.MaxReadAttempts.Value < config.Calibration.SampleCount.Value)
        {
            throw new InvalidOperationException(
                "calibration.maxReadAttempts must be greater than or equal to calibration.sampleCount.");
        }

        if (!IsSupportedUiLanguage(config.Ui.Language))
        {
            throw new InvalidOperationException("ui.language must be one of: auto, en, ru, es.");
        }
    }

    private static bool IsSupportedUiLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var normalized = language.Trim().ToLower(CultureInfo.InvariantCulture);
        return normalized is UiSettings.AutoLanguage or "en" or "ru" or "es";
    }

    private static void ValidateBrightnessCurve(IReadOnlyList<BrightnessCurvePoint> curve)
    {
        if (curve.Count < 2)
        {
            throw new InvalidOperationException("brightness.curve must contain at least two points.");
        }

        var seen = new HashSet<int>();
        foreach (var point in curve)
        {
            if (point.LightPercent is < 0 or > 100)
            {
                throw new InvalidOperationException("brightness.curve lightPercent must be in the range 0..100.");
            }

            if (point.BrightnessPercent is < 0 or > 100)
            {
                throw new InvalidOperationException("brightness.curve brightnessPercent must be in the range 0..100.");
            }

            if (!seen.Add(point.LightPercent))
            {
                throw new InvalidOperationException("brightness.curve cannot contain duplicate lightPercent values.");
            }
        }
    }
}
