using System.Diagnostics;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class FirmwareFlashService(string applicationDirectory) : IFirmwareFlashService
{
    private readonly string _applicationDirectory = applicationDirectory;

    public void Flash(BundledFirmwareInfo firmwareInfo, string portName)
    {
        ArgumentNullException.ThrowIfNull(firmwareInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        var esptoolPath = ResolveEsptoolPath();
        var startInfo = CreateStartInfo(esptoolPath, _applicationDirectory, firmwareInfo, portName);

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

    internal static ProcessStartInfo CreateStartInfo(string esptoolPath, string applicationDirectory, BundledFirmwareInfo firmwareInfo, string portName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(esptoolPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentNullException.ThrowIfNull(firmwareInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        if (!string.Equals(firmwareInfo.FlashMethod, "esptool", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Firmware flash method '{firmwareInfo.FlashMethod}' is not supported.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = esptoolPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = applicationDirectory
        };
        startInfo.ArgumentList.Add("--chip");
        startInfo.ArgumentList.Add(firmwareInfo.Chip);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(portName);
        startInfo.ArgumentList.Add("--baud");
        startInfo.ArgumentList.Add(firmwareInfo.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("write-flash");
        startInfo.ArgumentList.Add(firmwareInfo.Offset);
        startInfo.ArgumentList.Add(firmwareInfo.AbsolutePath);
        return startInfo;
    }

    private string ResolveEsptoolPath()
    {
        var bundledPath = Path.Combine(_applicationDirectory, "Tools", "esptool.exe");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        throw new InvalidOperationException(
            "esptool.exe was not found. Place it in the Tools folder beside the application.");
    }
}
