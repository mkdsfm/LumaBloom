# Build

This document covers the Windows companion app.

## Requirements

- Windows 10/11.
- .NET SDK 10.0+.

## Restore, Build, Test

From `pc-app/`:

```powershell
dotnet restore
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```

## Run

From `pc-app/`:

```powershell
dotnet run
```

The app opens a live terminal dashboard, discovers the ESP32-C6 serial device, reads raw telemetry, and applies monitor brightness through Windows APIs.

For detailed notes on how sensor values are read over `USB` and how monitor brightness is applied through `DDC/CI`, see [`sensor-transports-and-monitor-ddc.md`](sensor-transports-and-monitor-ddc.md).

## Portable Zip

From the repository root:

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag dev
```

## Single-File Windows Publish

From the repository root:

```powershell
dotnet publish pc-app/BrightnessSensor.ConsoleApp/BrightnessSensor.ConsoleApp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o pc-app/artifacts/single-file/win-x64 `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=None `
  /p:DebugSymbols=false
```

Output:

```text
pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe
```

The publish output does not include `appsettings.json`. On first run without an explicit config path, the app creates a minimal config beside the executable and persists UI settings there.

When the repo already contains an ESP32-C6 firmware release payload in `firmware/firmware_esp32c6/build/release/`, the portable release script copies that release folder into the publish folder under `Firmware/`. The in-app Update screen uses the bundled firmware payload from there.
