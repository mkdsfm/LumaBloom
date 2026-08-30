using System.Text.Json;
using BrightnessSensor.ConsoleApp.Application;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class BundledFirmwareLocatorTests
{
    [Fact]
    public void LocateAll_ReturnsSupportedManifestEntriesWithFullVersions()
    {
        var applicationDirectory = Path.Combine(Path.GetTempPath(), $"lumabloom-tests-{Guid.NewGuid():N}");
        var firmwareDirectory = Path.Combine(applicationDirectory, "Firmware");
        Directory.CreateDirectory(firmwareDirectory);

        try
        {
            CreateFirmware(firmwareDirectory, "lumabloom_1.2.0-preview.3_merged.bin", "1.2.0-preview.3", "esptool");
            CreateFirmware(firmwareDirectory, "lumabloom_1.1.0_merged.bin", "1.1.0", "esptool");
            CreateFirmware(firmwareDirectory, "other_3.0.0_merged.bin", "3.0.0", "vendor-tool");

            var located = new BundledFirmwareLocator().TryLocateAll(applicationDirectory, out var options, out _);

            Assert.True(located);
            Assert.Equal(2, options.Count);
            Assert.Contains(options, option => option.Version == "1.2.0-preview.3" && option.FileName == "lumabloom_1.2.0-preview.3_merged.bin");
            Assert.Contains(options, option => option.Version == "1.1.0" && option.FileName == "lumabloom_1.1.0_merged.bin");
            Assert.DoesNotContain(options, option => option.FileName == "other_3.0.0_merged.bin");
        }
        finally
        {
            Directory.Delete(applicationDirectory, recursive: true);
        }
    }

    private static void CreateFirmware(string directory, string fileName, string version, string flashMethod)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, [1]);
        var manifest = new
        {
            schemaVersion = 1,
            version,
            fileName,
            variant = "test-variant",
            board = "test-board",
            flashMethod,
            chip = "esp32c6",
            baudRate = 460800,
            offset = "0x0"
        };
        File.WriteAllText($"{path}.manifest.json", JsonSerializer.Serialize(manifest));
    }
}
