using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BrightnessSensor.ConsoleApp.Runtime;

namespace BrightnessSensor.ConsoleApp.Configuration;

internal static class AppConfigWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static void UpdateUiLanguage(string path, string languageCode)
    {
        var root = LoadRoot(path);
        var ui = GetOrCreateObject(root, "ui");
        ui["language"] = languageCode;
        SaveRoot(path, root);
    }

    public static void UpdateProcessing(string path, ProcessingParameter parameter, string value)
    {
        var root = LoadRoot(path);
        var processing = GetOrCreateObject(root, "processing");
        processing[GetProcessingPropertyName(parameter)] = parameter == ProcessingParameter.Invert
            ? bool.Parse(value)
            : parameter is ProcessingParameter.EmaAlpha or ProcessingParameter.Gamma
                ? double.Parse(value, CultureInfo.InvariantCulture)
                : int.Parse(value, CultureInfo.InvariantCulture);
        SaveRoot(path, root);
    }

    public static void UpdateBrightnessCurvePoint(string path, int lightPercent, int brightnessPercent)
    {
        var root = LoadRoot(path);
        var brightness = GetOrCreateObject(root, "brightness");
        var curve = NormalizeBrightnessCurve(brightness["curve"] as JsonArray, brightness);

        var updated = false;
        for (var i = 0; i < curve.Count; i++)
        {
            if (curve[i] is not JsonObject point ||
                point["lightPercent"]?.GetValue<int>() != lightPercent)
            {
                continue;
            }

            point["brightnessPercent"] = brightnessPercent;
            updated = true;
            break;
        }

        if (!updated)
        {
            curve.Add(new JsonObject
            {
                ["lightPercent"] = lightPercent,
                ["brightnessPercent"] = brightnessPercent
            });
        }

        var sorted = new JsonArray();
        foreach (var point in curve
                     .OfType<JsonObject>()
                     .OrderBy(point => point["lightPercent"]?.GetValue<int>() ?? 0))
        {
            sorted.Add(point.DeepClone());
        }

        brightness["curve"] = sorted;
        SaveRoot(path, root);
    }

    private static JsonArray NormalizeBrightnessCurve(JsonArray? curve, JsonObject brightness)
    {
        var existingPoints = curve?
            .OfType<JsonObject>()
            .Where(point => point["lightPercent"] is not null && point["brightnessPercent"] is not null)
            .ToArray() ?? [];

        if (existingPoints.Length >= 2)
        {
            var normalized = new JsonArray();
            foreach (var point in existingPoints)
            {
                normalized.Add(point.DeepClone());
            }

            return normalized;
        }

        var minPercent = brightness["minPercent"]?.GetValue<int>() ?? 10;
        var maxPercent = brightness["maxPercent"]?.GetValue<int>() ?? 100;
        return new JsonArray
        {
            CreateCurvePoint(0, minPercent),
            CreateCurvePoint(25, Interpolate(minPercent, maxPercent, 0.25)),
            CreateCurvePoint(50, Interpolate(minPercent, maxPercent, 0.50)),
            CreateCurvePoint(75, Interpolate(minPercent, maxPercent, 0.75)),
            CreateCurvePoint(100, maxPercent)
        };
    }

    private static JsonObject CreateCurvePoint(int lightPercent, int brightnessPercent)
    {
        return new JsonObject
        {
            ["lightPercent"] = lightPercent,
            ["brightnessPercent"] = brightnessPercent
        };
    }

    private static int Interpolate(int minPercent, int maxPercent, double ratio)
    {
        return (int)Math.Round(minPercent + ((maxPercent - minPercent) * ratio), MidpointRounding.AwayFromZero);
    }

    private static JsonObject LoadRoot(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Config file not found: {path}");
        }

        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        return root ?? throw new InvalidOperationException("Failed to parse appsettings.json.");
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[propertyName] = created;
        return created;
    }

    private static void SaveRoot(string path, JsonObject root)
    {
        File.WriteAllText(path, root.ToJsonString(WriteOptions));
    }

    private static string GetProcessingPropertyName(ProcessingParameter parameter)
    {
        return parameter switch
        {
            ProcessingParameter.AdcMin => "adcMin",
            ProcessingParameter.AdcMax => "adcMax",
            ProcessingParameter.Invert => "invert",
            ProcessingParameter.EmaAlpha => "emaAlpha",
            ProcessingParameter.HysteresisPercent => "hysteresisPercent",
            ProcessingParameter.MaxBrightnessStepPercent => "maxBrightnessStepPercent",
            ProcessingParameter.Gamma => "gamma",
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null)
        };
    }
}
