using System.Diagnostics;
using System.IO.Compression;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class ApplicationUpdateService(string applicationDirectory, string executablePath)
{
    public void PrepareAndLaunchUpdate(GitHubReleaseInfo release, GitHubReleaseClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(client);

        var stagingRoot = Path.Combine(Path.GetTempPath(), "LumaBloom", "app-update", release.Version);
        var zipPath = Path.Combine(stagingRoot, release.PackageName);
        var extractPath = Path.Combine(stagingRoot, "extracted");
        var scriptPath = Path.Combine(stagingRoot, "apply-update.ps1");

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        Directory.CreateDirectory(stagingRoot);
        client.DownloadToFile(release.PackageUrl, zipPath, cancellationToken);
        ZipFile.ExtractToDirectory(zipPath, extractPath);

        var newExecutablePath = Directory
            .EnumerateFiles(extractPath, Path.GetFileName(executablePath), SearchOption.AllDirectories)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(newExecutablePath))
        {
            throw new InvalidOperationException("Downloaded release package does not contain the application executable.");
        }

        var updateSourceDirectory = Path.GetDirectoryName(newExecutablePath);
        if (string.IsNullOrWhiteSpace(updateSourceDirectory))
        {
            throw new InvalidOperationException("Downloaded release package has an invalid folder layout.");
        }

        File.WriteAllText(scriptPath, BuildPowerShellScript(updateSourceDirectory, applicationDirectory, executablePath));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -WaitForPid {Environment.ProcessId}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = applicationDirectory
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to launch the application update helper.");
        }
    }

    private static string BuildPowerShellScript(string sourceDirectory, string targetDirectory, string executablePath)
    {
        var executableName = Path.GetFileName(executablePath);

        return $$"""
param(
    [int]$WaitForPid
)

$ErrorActionPreference = "Stop"

try {
    Wait-Process -Id $WaitForPid
} catch {
}

$source = "{{sourceDirectory}}"
$target = "{{targetDirectory}}"
$exeName = "{{executableName}}"

Get-ChildItem -Path $source -Force | ForEach-Object {
    $destination = Join-Path $target $_.Name
    if ($_.PSIsContainer) {
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }
    }
}

Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
Start-Process -FilePath (Join-Path $target $exeName) -WorkingDirectory $target -WindowStyle Normal
""";
    }
}
