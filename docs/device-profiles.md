# Device Settings

`pc-app` now uses one built-in LumaBloom settings baseline instead of multiple hardware profiles.

## How It Works

1. The app probes available COM ports.
2. It accepts the first port that emits valid JSON telemetry matching the LumaBloom protocol:
   `{"id":"lumabloom","ts":1234567,"raw":1840}`
3. It resolves effective runtime settings as:
   `built-in defaults + appsettings.json overrides`
4. It applies brightness using the live `raw` sensor value and the active response curve.

There is no longer any runtime profile selection based on `deviceId + sensorId`.

## Built-In Defaults

The single built-in baseline matches the current ESP32-C6 + KY-018 setup:

- `measurementKind=Adc`
- `connection.baudRate=115200`
- `connection.discoveryTimeoutMs=2500`
- `processing.adcMin=200`
- `processing.adcMax=3200`
- `processing.invert=true`
- `processing.emaAlpha=0.2`
- `processing.hysteresisPercent=1`
- `processing.maxBrightnessStepPercent=2`
- `processing.gamma=1.0`
- `brightness.minPercent=10`
- `brightness.maxPercent=100`
- default curve points at `0/25/50/75/100`

## User Config

You can keep `appsettings.json` minimal:

```json
{
  "brightness": {
    "minPercent": 10,
    "maxPercent": 100
  }
}
```

For a full example with every common override populated, see [appsettings.esp32c6.example.json](../pc-app/appsettings.esp32c6.example.json).

## Config Parameters

Top-level sections:

- `connection` - optional serial transport overrides
- `processing` - optional raw-signal processing overrides
- `brightness` - optional output brightness bounds and response curve
- `ui` - runtime UI preferences

### `connection`

- `connection.baudRate` (`number`, optional): must be greater than `0`
- `connection.discoveryTimeoutMs` (`number`, optional): must be greater than `0`

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

## Brightness Curve Editing

The response curve is stored in `brightness.curve` as a sorted list of `{ lightPercent, brightnessPercent }` points.

Important behavior:

- The settings screen still presents the familiar five visible curve positions at `0/25/50/75/100`.
- The `Current light` action in the UI is additive: it uses the latest live ambient reading as an anchor and rebuilds the stored curve around that point.
- The current ambient percent for this action is derived from the live `raw` sensor field together with the active `processing.adcMin`, `processing.adcMax`, and `processing.invert` values.
- When needed, the saved curve may contain extra points beyond the base five, for example `{ "lightPercent": 16, "brightnessPercent": 60 }`.
- Extra anchor points are valid configuration and are used by interpolation so that the chosen current-light target is preserved exactly.
