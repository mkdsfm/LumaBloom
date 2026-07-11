using System.Diagnostics;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class FirmwareFlashService(string applicationDirectory)
{
    private readonly string _applicationDirectory = applicationDirectory;

    public void Flash(BundledFirmwareInfo firmwareInfo, string portName)
    {
        ArgumentNullException.ThrowIfNull(firmwareInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        var esptoolPath = ResolveEsptoolPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = esptoolPath,
            Arguments =
                $"--chip {firmwareInfo.Chip} --port {portName} --baud 460800 write-flash 0x0 \"{firmwareInfo.AbsolutePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _applicationDirectory
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to launch esptool.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"Firmware flashing failed with exit code {process.ExitCode}. {details}".Trim());
        }
    }

    private string ResolveEsptoolPath()
    {
        var candidates = new[]
        {
            Path.Combine(_applicationDirectory, "Tools", "esptool.exe"),
            @"C:\Espressif\tools\python\v6.0\venv\Scripts\esptool.exe",
            "esptool.exe"
        };

        foreach (var candidate in candidates)
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            "esptool.exe was not found. Place it in the Tools folder beside the application.");
    }
}
