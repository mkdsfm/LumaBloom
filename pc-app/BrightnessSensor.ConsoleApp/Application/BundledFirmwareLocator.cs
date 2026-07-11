using System.Text.RegularExpressions;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class BundledFirmwareLocator
{
    private static readonly Regex VersionPattern = new(@"(?<!\d)(\d+\.\d+\.\d+)(?!\d)", RegexOptions.Compiled);

    public bool TryLocate(string applicationDirectory, out BundledFirmwareInfo? firmwareInfo, out string statusMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        var firmwareDirectory = Path.Combine(applicationDirectory, "Firmware");
        if (!Directory.Exists(firmwareDirectory))
        {
            firmwareInfo = null;
            statusMessage = "Bundled firmware folder was not found in the application package.";
            return false;
        }

        var candidates = Directory
            .EnumerateFiles(firmwareDirectory, "*.bin", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith("_merged.bin", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => Score(file.Name))
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        if (candidates.Length == 0)
        {
            firmwareInfo = null;
            statusMessage = "Bundled firmware file was not found in the application package.";
            return false;
        }

        var selected = candidates[0];
        var version = ExtractVersion(selected.Name) ?? AppVersion.Current;

        firmwareInfo = new BundledFirmwareInfo(
            version,
            "esp32c6",
            "waveshare-esp32-c6-lcd-1.47",
            selected.Name,
            selected.FullName);
        statusMessage = $"Bundled firmware {version} is ready.";
        return true;
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

    private static string? ExtractVersion(string fileName)
    {
        var match = VersionPattern.Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }
}
