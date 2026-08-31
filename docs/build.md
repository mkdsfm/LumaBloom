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

`BrightnessSensor.ConsoleApp` versioning is resolved from git tags during build via `MinVer`. A tagged commit such as `1.4.0` produces that exact app version; branch builds between tags produce a prerelease version automatically.

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
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py
```

For a truly portable firmware-update bundle, place the official standalone Windows `esptool.exe` at:

```text
third_party/esptool/win-x64/esptool.exe
```

The portable zip script copies that file into `Tools/esptool.exe` beside the app. If the repo-local standalone binary is missing, the script skips bundling `esptool.exe` so the packaging problem is visible immediately instead of silently depending on a developer machine.

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

The in-app application update flow also preserves an existing `appsettings.json` in the installed app folder, so user settings survive portable-package upgrades.

Before publishing, the portable release script discovers every immediate project under `firmware/` and runs its required project-local `build_merged.py`. Each project owns its toolchain and must emit all supported variants as `build/release/*_<tag>_merged.bin` with a `<binary>.manifest.json` sidecar. Old outputs for the target version are removed first. The portable package copies every matching artifact and manifest into `Firmware/`; the in-app Update screen lists entries whose manifest declares a supported flashing method.

The Update screen selects the automatically discovered LumaBloom COM port by default. Its firmware-port dropdown rescans `SerialPort.GetPortNames()` every time it opens, so the user can select a newly connected or alternative port without restarting the app. Entries include the Windows device name when available; native Espressif USB devices are marked `Espressif/ESP32`, and the validated telemetry port is marked as the automatic LumaBloom choice. Firmware version and port selections apply only to the current app session and are not persisted to `appsettings.json`.
