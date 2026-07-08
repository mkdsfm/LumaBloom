# Device Profiles

`pc-app` resolves hardware-specific defaults from built-in device profiles after the first valid telemetry message.

## How It Works

1. The app resolves a COM port.
2. It reads the first valid JSON telemetry message.
3. It matches `deviceId + sensorId` to a built-in profile.
4. It builds effective runtime settings as `profile defaults + appsettings overrides`.
5. If nothing matches, it logs a warning and falls back to `generic-adc-safe`.

## Built-In Profiles

- `esp32c6-analog-ky018` for `deviceId=esp32c6-01`, `sensorId=light0`, measurement kind `Adc`
- `generic-adc-safe` as a fallback profile

## User Config

You can keep `appsettings.json` minimal:

```json
{
  "deviceProfile": {
    "autoDetect": true
  },
  "brightness": {
    "minPercent": 10,
    "maxPercent": 100
  }
}
```

For a full debugging-oriented example with every optional field populated, see [appsettings.full.example.json](../appsettings.full.example.json).

## Config Parameters

Top-level sections:

- `serial` - optional COM discovery and serial-read overrides
- `deviceProfile` - profile auto-detect or forced profile selection
- `processing` - optional raw-signal processing overrides
- `brightness` - optional output brightness bounds and response curve
- `ui` - runtime UI preferences

### `serial`

- `serial.deviceId` (`string`, optional): restrict discovery to telemetry from one device id, for example `esp32c6-01`
- `serial.baudRate` (`number`, optional): must be `> 0`; default is the resolved profile baud rate, currently `115200`
- `serial.discoveryTimeoutMs` (`number`, optional): must be `> 0`; default is the resolved profile timeout, currently `2500`

### `deviceProfile`

- `deviceProfile.autoDetect` (`bool`): defaults to `true`
- `deviceProfile.profileId` (`string`, optional): required when `autoDetect=false`

### `processing`

- `processing.adcMin` (`number`, optional): must be less than `processing.adcMax`
- `processing.adcMax` (`number`, optional): must be greater than `processing.adcMin`
- `processing.invert` (`bool`, optional): use `true` when more ambient light produces a lower raw ADC value
- `processing.emaAlpha` (`number`, optional): valid range `(0, 1]`
- `processing.hysteresisPercent` (`number`, optional): valid range `0..100`
- `processing.maxBrightnessStepPercent` (`number`, optional): valid range `1..100`
- `processing.gamma` (`number`, optional): must be `> 0`

### `brightness`

- `brightness.minPercent` (`number`, optional): valid range `0..100`
- `brightness.maxPercent` (`number`, optional): valid range `0..100`, and must be `>= minPercent`
- `brightness.curve` (`array`, optional): sorted or unsorted list of `{ "lightPercent": 0..100, "brightnessPercent": 0..100 }`

Curve validation:

- at least 2 points are required if `brightness.curve` is present
- `lightPercent` values must be unique
- extra anchor points are valid and may be written by the UI in addition to the base `0/25/50/75/100` points

### `ui`

- `ui.language` (`string`): one of `auto`, `en`, `ru`, `es`

Optional overrides:

- `serial.deviceId` to narrow COM port discovery to one device
- `serial.baudRate` and `serial.discoveryTimeoutMs` only when you need to override built-in defaults
- `deviceProfile.profileId` to force a profile for debugging
- `deviceProfile.autoDetect=false` together with `deviceProfile.profileId`
- partial `processing` and `brightness` overrides

For `esp32c6-analog-ky018` specifically:

- `processing.adcMin=200` and `processing.adcMax=3200` describe the practical raw KY-018 range used by both the LCD and the Windows app;
- `processing.invert` should stay `true`, because lower raw values mean brighter ambient light on this wiring;
- `processing.gamma=1.0`, `processing.emaAlpha=0.2`, `processing.hysteresisPercent=1`, and `processing.maxBrightnessStepPercent=2` are the current built-in defaults.

## Brightness Curve Editing

The response curve is stored in `brightness.curve` as a sorted list of `{ lightPercent, brightnessPercent }` points.

Important behavior:

- The settings screen still presents the familiar five visible curve positions at `0/25/50/75/100`.
- The `Current light` action in the UI is additive: it uses the latest live ambient reading as an anchor and rebuilds the stored curve around that point.
- For `esp32c6-analog-ky018`, the current ambient percent for this action is derived from the live `raw` sensor field together with the active `processing.adcMin`, `processing.adcMax`, and `processing.invert` values.
- When needed, the saved curve may contain extra points beyond the base five, for example `{ "lightPercent": 16, "brightnessPercent": 60 }`.
- Extra anchor points are valid configuration and are used by interpolation so that the chosen current-light target is preserved exactly.

## Adding a New Profile

1. Add a new entry to `pc-app/BrightnessSensor.ConsoleApp/Profiles/DeviceProfileCatalog.cs`.
2. Set `profileId`, `deviceId`, `sensorId`, measurement kind, and recommended defaults.
3. Add or update tests in `pc-app/BrightnessSensor.ConsoleApp.Tests/AppConfigLoaderTests.cs`.
