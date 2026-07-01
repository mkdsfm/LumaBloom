# Build and Run

## Firmware (ESP32-C6, ESP-IDF, KY-018 + LCD 1.47)

Requirements:

- ESP-IDF 6.x
- Waveshare `ESP32-C6-LCD-1.47` board
- `KY-018` sensor

Default KY-018 wiring in the project:

- `KY-018 VCC` -> `3V3`
- `KY-018 GND` -> `GND`
- `KY-018 AO` -> `GPIO4` (ADC)

If you connected the sensor to a different ADC pin, update the constants in `firmware/firmware_esp32c6/main/app_config.h`.
Using `GPIO0` on `ESP32-C6` is not recommended because it may interfere with normal board startup when the sensor is attached.

Steps:

1. Open an ESP-IDF terminal.
2. Go to `firmware/firmware_esp32c6`.
3. Set the target:

```powershell
idf.py set-target esp32c6
```

4. Open configuration if needed:

```powershell
idf.py menuconfig
```

5. Build the project:

```powershell
idf.py build
```

6. Flash and open the monitor:

```powershell
idf.py -p COMx flash monitor
```

Expected result:

- the LCD shows a centered large percentage value with a smaller centered `ADC ####` line below it;
- before `pc-app` calibration, the LCD shows `--%` while telemetry keeps `calibrated=false`;
- after calibration, the serial monitor receives JSON lines with `deviceId`, `sensorId`, `ts`, `value`, `raw`, and `calibrated`;
- the Windows application from `pc-app/` calibrates the device at startup and then uses normalized `0..1000` readings.

If you use Codex, an automated skill-based workflow for release binary creation and flashing is documented in [skills-for-users.md](skills-for-users.md).

## PC Application (.NET)

Requirements:

- Windows 10/11
- .NET SDK 10.0+

Preparation:

1. Create `pc-app/appsettings.json` from `appsettings.esp32c6.example.json` for ESP32-C6 + KY-018.
2. Optionally set `serial.deviceId` if you want to limit autodiscovery to one specific device. If this field is not set, the app uses the first COM port with valid telemetry.
3. Only add the overrides you actually need, such as brightness limits, a forced `deviceProfile.profileId` for debugging, or selected `processing` / `calibration` fields on top of the built-in profile.

Run from `pc-app/`:

```powershell
dotnet restore
dotnet run
```

The application starts a live Terminal.Gui dashboard. It automatically finds the COM port, reads valid messages, selects a built-in hardware profile by `deviceId + sensorId`, shows the effective settings and runtime status, computes the target brightness, and applies it through WMI to brightness-capable monitors.

If the sensor is not connected yet, the UI stays open in `WAITING` state and keeps trying to discover/reopen the device. The same waiting/reconnect loop is used after disconnecting and reconnecting the sensor.

Runtime UI:

- `Overview` - normal runtime status and Auto/Manual brightness controls.
- `Settings` - language, autostart, brightness curve, and response tuning.
- `Events` - recent runtime event log.
- `Diagnostics` - raw telemetry, profile, connection, and monitor details.

Navigation:

- Arrow keys - move between screens and visible controls.
- Enter - activate the focused control.
- Esc - cancel or go back.
- Mouse click - activate visible tabs and controls when supported by the terminal.
- Ctrl+C - request shutdown.

The terminal UI supports English, Russian, and Spanish. Set `ui.language` in `appsettings.json` to `auto`, `en`, `ru`, or `es`, or choose a language from `Settings / General`.

Settings sections:

- `Settings / General` opens by default. It contains language selection and Windows autostart.
- `Settings / Calibration` edits the brightness curve. The top table row is ambient light in the room, matching the value shown on the display. The bottom row is the monitor brightness LumaBloom should use. Values between points are interpolated smoothly, not applied as thresholds.
- `Settings / Response` / `Реагирование` edits `processing` overrides through validated modals and applies confirmed changes without restarting the app. `invert` is edited with true/false radio-style controls.

For `ESP32-C6`, startup behavior is:

1. `pc-app` reads the first valid telemetry messages and collects several raw samples.
2. It reads the current monitor brightness from Windows.
3. It sends `{"type":"calibrate", ...}` to the device over the same COM port.
4. After the device replies with `calibrationResult`, the main runtime loop starts using normalized `0..1000` values.

## Build And Test

Run from `pc-app/`:

```powershell
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```

## Portable And Single-File Builds

Portable zip from repo root:

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag dev
```

Single-file exe from repo root:

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

The single-file output is:

- `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`

The publish output does not include `appsettings.json`. On first run without an explicit config path, the app creates a minimal `appsettings.json` beside the executable and then persists UI settings there.

For the list of built-in profiles and instructions for adding a new one, see `docs/device-profiles.md`.

Important: the implementation in `pc-app/` is Windows-only. Other operating systems would need a separate application that supports the same device communication contract, meaning JSON lines defined by `docs/protocol.md`.
