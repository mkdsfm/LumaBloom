# brightness-sensor

A bundle of firmware and a Windows console application for automatically adjusting display brightness based on an ambient light sensor.

<img width="964" height="1280" alt="image" src="https://github.com/user-attachments/assets/c9080153-6f3e-4f87-9e20-5d9c1311d273" />

## Contents

- `firmware/firmware_esp32c6/` - ESP-IDF project for Waveshare ESP32-C6-LCD-1.47 with KY-018, onboard LCD, and runtime calibration from `pc-app`.
- `hardware/` - wiring, BOM, hardware revisions, and the printable LumaBloom enclosure assembly for the ESP32-C6 build.
- `pc-app/` - Windows-only .NET application with a live console dashboard that reads JSON from a COM port and controls brightness through WMI.
- `docs/` - protocol, device profiles, and build/run instructions.
- `appsettings.example.json` - example configuration for the PC application.
- `appsettings.full.example.json` - full example with all optional override parameters.
- `pc-app/appsettings.esp32c6.example.json` - example configuration for ESP32-C6 + KY-018.

## Quick Start

1. Build and flash the controller. See `docs/build-and-run.md`.
2. Wire the sensor. See `hardware/WIRING.md`.
3. Copy the appropriate example to `pc-app/appsettings.json`:
   `pc-app/appsettings.esp32c6.example.json` for a minimal ESP32-C6 + KY-018 setup,
   or `appsettings.full.example.json` for a full template with all optional fields.
4. Start the PC application from `pc-app/`.
   It opens a live terminal dashboard with tabs, arrow-key navigation, and mouse-clickable actions.
5. For `ESP32-C6`, wait for startup calibration to complete. Until then the device LCD shows `--%` plus the current `ADC` line, and telemetry keeps `calibrated=false`.

Important: the current PC application supports Windows only. Linux and macOS would require a separate application that preserves the same device communication contract, meaning the same JSON protocol from `docs/protocol.md`.

## `pc-app/` Runtime UI

The Windows application opens a rich terminal UI with top-level screens:

- `Overview` - normal runtime status and primary actions.
- `Settings` - language, autostart, brightness curve, and response tuning.
- `Events` - recent runtime event log.
- `Diagnostics` - raw telemetry, profile, connection, and monitor details.

Navigation:

- Arrow keys - move between screens and visible controls.
- Enter - activate the focused control.
- Esc - cancel, go back, or return to a safe default.
- Mouse click - activate visible tabs and controls when supported by the terminal.
- Ctrl+C - request shutdown.

`Settings` contains:

- `General` - language selection for English, Russian, and Spanish, plus Windows autostart.
- `Calibration` - brightness curve points that map ambient light percent to display brightness percent; values between points are interpolated smoothly.
- `Response` / `Реагирование` - modal editors for `processing` overrides such as `emaAlpha`, `hysteresisPercent`, and `gamma`.

The app also supports a single-file Windows publish. On first run without a bundled config, it creates a minimal `appsettings.json` next to the executable and then persists user settings there.

## ESP32-C6 Behavior

`firmware_esp32c6` behavior:

- the firmware sends `value` as a normalized calibrated value in the `0..1000` range;
- it also sends `raw` as a diagnostic/raw ADC field for startup calibration;
- `pc-app` must send a `calibrate` command after reading the current monitor brightness;
- before calibration, the device publishes `value=0` while the LCD keeps the percentage placeholder at `--%`.

## Hardware

Hardware documentation:

| Path | Purpose |
| --- | --- |
| `hardware/README.md` | Hardware section index |
| `hardware/WIRING.md` | KY-018 wiring for ESP32-C6 |
| `hardware/BOM.md` | Bill of materials |
| `hardware/ASSEMBLY.md` | ESP32-C6 enclosure assembly steps and smoke checks |
| `hardware/REVISIONS.md` | Hardware revision log |
| `hardware/3d-print/README.md` | Enclosure and 3D-print notes |
| `hardware/3d-print/enclosure/` | Slicer-ready `.3mf` plates grouped by color |
| `hardware/3d-print/images/` | Print preparation and assembly-reference images |
| `hardware/3d-print/source/` | STEP source models and selected STL exports |

## `pc-app/` Structure

- `pc-app/Program.cs` - entry point.
- `pc-app/Application/` - main application workflow.
- `pc-app/Configuration/` - `appsettings.json` loading and validation.
- `pc-app/BrightnessSensor.BrightnessMath/` - separate library with math for mapping sensor readings to screen brightness.
- `pc-app/BrightnessSensor.DeviceReading/` - separate library for reading device data and parsing telemetry.
- `pc-app/BrightnessSensor.WindowsBrightness/` - separate library for Windows-specific monitor brightness control.

## Expected Configuration

The PC application expects an `appsettings.json` file. When no explicit config path is passed and the default file is missing, the app creates a minimal config beside the executable.

- `serial`
  - `deviceId` - optional device identifier from JSON telemetry.
    Effect: if set, the app looks for the COM port of that exact device; if not set, it chooses the first port with valid telemetry.
  - `baudRate` - optional port speed override.
    Effect: by default this comes from the built-in profile; only set it when you need to override the built-in value.
  - `discoveryTimeoutMs` - optional timeout override for probing one COM port.
    Effect: by default this comes from the built-in profile; only set it when you need a non-standard timeout.
- `deviceProfile`
  - `autoDetect` - enables automatic selection of a built-in hardware profile from the first valid messages.
  - `profileId` - optionally forces a built-in profile for debugging.
- `processing`
  - All fields are optional and work as overrides on top of the built-in profile.
  - These values can also be edited from `Settings / Response` / `Реагирование`; confirmed edits are saved to `appsettings.json` and applied without restart.
  - `adcMin` - lower bound of the ADC range.
  - `adcMax` - upper bound of the ADC range; `adcMax > adcMin` is required.
  - `invert` - inverts the scale (`true/false`).
  - `emaAlpha` - EMA coefficient in the `(0; 1]` range.
  - `hysteresisPercent` - minimum brightness change step in percent (`0..100`).
  - `maxBrightnessStepPercent` - maximum brightness delta per update (`1..100`).
  - `gamma` - optional gamma correction after EMA (`null` disables it, typically `1.8..2.2`).
- `brightness`
  - Fields work as user overrides for the final brightness range.
  - `minPercent` - lower brightness bound (`0..100`).
  - `maxPercent` - upper brightness bound (`0..100`, `min <= max`).
  - `curve` - optional points mapping normalized ambient light to display brightness.
    Each point has `lightPercent` and `brightnessPercent` in `0..100`; at least two unique `lightPercent` values are required.
    Effect: the app linearly interpolates between neighboring points, so brightness changes smoothly instead of jumping between thresholds.
- `calibration`
  - All fields are optional and work as overrides on top of the built-in profile.
  - `enabled` - enables firmware/device calibration on startup (`true/false`).
  - `sampleCount` - number of valid measurements to average (`>= 1`).
  - `maxReadAttempts` - maximum sensor read attempts (`>= sampleCount`).
- `ui`
  - `language` - terminal UI language: `auto`, `en`, `ru`, or `es`.
    Effect: `auto` uses the OS culture when supported and falls back to English.
    The language can also be selected from `Settings / General` and is saved to this field.

Examples:

- `appsettings.example.json` - minimal user config.
- `pc-app/appsettings.esp32c6.example.json` - minimal config for ESP32-C6 + KY-018.
- `appsettings.full.example.json` - full template with all optional fields and overrides.

For details on built-in profiles, the generic fallback, and how to add new profiles, see `docs/device-profiles.md`.

## Built-In Profiles

- `esp32c6-analog-ky018` - `deviceId=esp32c6-01`, `sensorId=light0`, measurement type `Normalized1000`.
- `generic-adc-safe` - fallback profile for unknown devices, measurement type `ADC`.

## Telemetry Format

Each measurement is sent as a single JSON line, for example:

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":742,"raw":1840,"calibrated":true}`

`deviceId` is used by the PC application for COM port autodiscovery when `serial.deviceId` is set. After that, `deviceId + sensorId` is used to select the built-in hardware profile.

ESP-IDF firmware for ESP32-C6 sends the calibrated normalized `value` in the `0..1000` range.

Details: `docs/protocol.md`.

## Codex Skills

If you use Codex in this repo, see [docs/skills-for-users.md](docs/skills-for-users.md) for the recommended user-facing workflow for skills such as building and flashing the ESP32 release firmware.
