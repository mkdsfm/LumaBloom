namespace BrightnessSensor.ConsoleApp.Application;

internal static class ApplicationUpdateScriptBuilder
{
    private static readonly string[] PreservedFileNames = ["appsettings.json"];

    public static string Build(string sourceDirectory, string targetDirectory, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var executableName = Path.GetFileName(executablePath);
        var preservedFilesLiteral = string.Join(
            ", ",
            PreservedFileNames.Select(name => $"'{name.Replace("'", "''", StringComparison.Ordinal)}'"));

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
$preservedFiles = @({{preservedFilesLiteral}})

Get-ChildItem -Path $source -Force | ForEach-Object {
    $destination = Join-Path $target $_.Name
    if ($_.PSIsContainer) {
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }

        return
    }

    if (($preservedFiles -contains $_.Name) -and (Test-Path -LiteralPath $destination)) {
        return
    }

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}

Start-Process -FilePath (Join-Path $target $exeName) -WorkingDirectory $target -WindowStyle Normal
""";
    }
}
