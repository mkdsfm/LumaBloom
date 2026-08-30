using System.Text.Json;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class BundledFirmwareLocator
{
    private const string SupportedFlashMethod = "esptool";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool TryLocateAll(string applicationDirectory, out IReadOnlyList<BundledFirmwareInfo> firmwareOptions, out string statusMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        var firmwareDirectory = Path.Combine(applicationDirectory, "Firmware");
        if (!Directory.Exists(firmwareDirectory))
        {
            firmwareOptions = [];
            statusMessage = "Bundled firmware folder was not found in the application package.";
            return false;
        }

        var candidates = Directory
            .EnumerateFiles(firmwareDirectory, "*.bin", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Select(TryReadFirmwareInfo)
            .Where(info => info is not null)
            .Select(info => info!)
            .OrderByDescending(info => Score(info.FileName))
            .ThenByDescending(info => File.GetLastWriteTimeUtc(info.AbsolutePath))
            .ToArray();

        if (candidates.Length == 0)
        {
            firmwareOptions = [];
            statusMessage = "No supported firmware with a valid manifest was found in the application package.";
            return false;
        }

        firmwareOptions = candidates;
        statusMessage = $"Found {firmwareOptions.Count} bundled firmware file(s). Selected {firmwareOptions[0].Version}.";
        return true;
    }

    private static BundledFirmwareInfo? TryReadFirmwareInfo(FileInfo file)
    {
        var manifestPath = $"{file.FullName}.manifest.json";
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<FirmwareArtifactManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null || manifest.SchemaVersion != 1 ||
                !string.Equals(manifest.FileName, file.Name, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.FlashMethod, SupportedFlashMethod, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Variant) ||
                string.IsNullOrWhiteSpace(manifest.Board) || string.IsNullOrWhiteSpace(manifest.Chip) ||
                manifest.BaudRate <= 0 || string.IsNullOrWhiteSpace(manifest.Offset))
            {
                return null;
            }

            return new BundledFirmwareInfo(
                manifest.Version,
                manifest.FlashMethod,
                manifest.Chip,
                manifest.BaudRate,
                manifest.Offset,
                file.Name,
                file.FullName);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static int Score(string fileName)
    {
        var score = 0;
        if (fileName.Contains(AppVersion.Current, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (fileName.EndsWith("_merged.bin", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }
}
