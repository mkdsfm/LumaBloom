# brightness-sensor

A bundle of firmware and a simple Windows console application for automatically adjusting the built-in display brightness based on an ambient light sensor.

## Contents

- `firmware/firmware_esp32c3/` - Arduino firmware for ESP32-C3, plus build and flashing instructions.
- `firmware/firmware_esp32c6/` - ESP-IDF project for Waveshare ESP32-C6-LCD-1.47 with KY-018, onboard LCD, and runtime calibration from `pc-app`.
- `pc-app/` - Windows-only .NET application that reads JSON from a COM port and controls brightness through WMI.
- `docs/` - wiring, protocol, and run instructions.
- `appsettings.example.json` - example configuration for the PC application.
- `appsettings.full.example.json` - full example with all optional override parameters.
- `pc-app/appsettings.esp32c6.example.json` - example configuration for ESP32-C6 + KY-018.

## Quick Start

1. Build and flash the controller. See `docs/build-and-run.md`.
2. Wire the sensor. See `docs/wiring.md`.
3. Copy the appropriate example to `pc-app/appsettings.json`:
   `appsettings.example.json` for a minimal ESP32-C3 setup,
   `pc-app/appsettings.esp32c6.example.json` for a minimal ESP32-C6 + KY-018 setup,
   or `appsettings.full.example.json` for a full template with all optional fields.
4. Start the PC application from `pc-app/`.
5. For `ESP32-C6`, wait for startup calibration to complete. Until then the device LCD shows `UNCAL` and telemetry keeps `calibrated=false`.

Important: the current PC application supports Windows only. Linux and macOS would require a separate application that preserves the same device communication contract, meaning the same JSON protocol from `docs/protocol.md`.

## ESP32-C6 Behavior

`firmware_esp32c6` differs from the simple ESP32-C3 flow:

- the firmware sends `value` as a normalized calibrated value in the `0..1000` range;
- it also sends `raw` as a diagnostic/raw ADC field for startup calibration;
- `pc-app` must send a `calibrate` command after reading the current monitor brightness;
- before calibration, the device remains in `UNCAL` state and publishes `value=0`.

## `pc-app/` Structure

- `pc-app/Program.cs` - entry point.
- `pc-app/Application/` - main application workflow.
- `pc-app/Configuration/` - `appsettings.json` loading and validation.
- `pc-app/BrightnessSensor.BrightnessMath/` - separate library with math for mapping sensor readings to screen brightness.
- `pc-app/BrightnessSensor.DeviceReading/` - separate library for reading device data and parsing telemetry.
- `pc-app/BrightnessSensor.WindowsBrightness/` - separate library for Windows-specific monitor brightness control.

## Expected Configuration

The PC application expects a `pc-app/appsettings.json` file with the following sections:

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
  - `adcMin` - lower bound of the ADC range.
    Effect: values below it are clamped to `adcMin`, which defines the start of the normalization scale.
  - `adcMax` - upper bound of the ADC range; `adcMax > adcMin` is required.
    Effect: values above it are clamped to `adcMax`, which defines the end of the normalization scale.
  - `invert` - inverts the scale (`true/false`).
    Effect: reverses the mapping direction, useful when the sensor logic is effectively upside down.
  - `emaAlpha` - EMA coefficient in the `(0; 1]` range.
    Effect: higher values react faster, lower values make brightness changes smoother and slower.
  - `hysteresisPercent` - minimum brightness change step in percent (`0..100`).
    Effect: suppresses small fluctuations by ignoring tiny changes.
  - `gamma` - optional gamma correction after EMA (`null` disables it, typically `1.8..2.2`).
    Effect: makes the brightness curve feel more natural, softer in dark areas and less abrupt overall.
- `brightness`
  - Fields work as user overrides for the final brightness range.
  - `minPercent` - lower brightness bound (`0..100`).
    Effect: brightness will not go below this value even in darkness.
  - `maxPercent` - upper brightness bound (`0..100`, `min <= max`).
    Effect: brightness will not exceed this value even in strong light.
- `calibration`
  - All fields are optional and work as overrides on top of the built-in profile.
  - `enabled` - enables calibration on startup (`true/false`).
    Effect: the initial screen brightness and sensor readings are treated as the ideal baseline.
  - `sampleCount` - number of valid measurements to average (`>= 1`).
    Effect: higher values give a more stable baseline but make calibration take longer.
  - `maxReadAttempts` - maximum sensor read attempts (`>= sampleCount`).
    Effect: limits how long calibration waits when data is missing.

Examples:

- `appsettings.example.json` - minimal user config
- `pc-app/appsettings.esp32c6.example.json` - minimal config for ESP32-C6 + KY-018
- `appsettings.full.example.json` - full template with all optional fields and overrides

For details on built-in profiles, the generic fallback, and how to add new profiles, see `docs/device-profiles.md`.

## Built-In Profiles

- `esp32c3-analog-ky018` - `deviceId=esp32c3-01`, `sensorId=light0`, measurement type `ADC`
- `esp32c6-analog-ky018` - `deviceId=esp32c6-01`, `sensorId=light0`, measurement type `Normalized1000`
- `generic-adc-safe` - fallback profile for unknown devices, measurement type `ADC`

## Telemetry Format

Each measurement is sent as a single JSON line, for example:

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":742,"raw":1840,"calibrated":true}`

`deviceId` is used by the PC application for COM port autodiscovery when `serial.deviceId` is set. After that, `deviceId + sensorId` is used to select the built-in hardware profile.

The `value` field depends on the firmware type:

- Arduino firmware for ESP32-C3 sends the raw ADC value.
- ESP-IDF firmware for ESP32-C6 sends the calibrated normalized value in the `0..1000` range.

Details: `docs/protocol.md`.

## Codex Skills

If you use Codex in this repo, see [docs/skills-for-users.md](docs/skills-for-users.md) for the recommended user-facing workflow for skills such as building and flashing the ESP32 release firmware.
