using BrightnessSensor.ConsoleApp.Application;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class ApplicationUpdateScriptBuilderTests
{
    [Fact]
    public void Build_PreservesExistingAppSettingsDuringUpdate()
    {
        var script = ApplicationUpdateScriptBuilder.Build(
            @"C:\temp\update",
            @"C:\apps\LumaBloom",
            @"C:\apps\LumaBloom\BrightnessSensor.ConsoleApp.exe");

        Assert.Contains("$preservedFiles = @('appsettings.json')", script, StringComparison.Ordinal);
        Assert.Contains(
            "if (($preservedFiles -contains $_.Name) -and (Test-Path -LiteralPath $destination)) {",
            script,
            StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $_.FullName -Destination $destination -Force", script, StringComparison.Ordinal);
    }
}
