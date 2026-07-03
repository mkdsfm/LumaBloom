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

Optional overrides:

- `serial.deviceId` to narrow COM port discovery to one device
- `serial.baudRate` and `serial.discoveryTimeoutMs` only when you need to override built-in defaults
- `deviceProfile.profileId` to force a profile for debugging
- `deviceProfile.autoDetect=false` together with `deviceProfile.profileId`
- partial `processing`, `brightness`, and `calibration` overrides

For `esp32c6-analog-ky018` specifically:

- `processing.adcMin=200` and `processing.adcMax=3200` describe the practical raw KY-018 range used by both the LCD and the Windows app;
- `processing.invert` should stay `true`, because lower raw values mean brighter ambient light on this wiring;
- startup calibration is disabled for this profile, because the Windows app now maps brightness directly from the sensor raw field and the configured response curve.

## Adding a New Profile

1. Add a new entry to `pc-app/BrightnessSensor.ConsoleApp/Profiles/DeviceProfileCatalog.cs`.
2. Set `profileId`, `deviceId`, `sensorId`, measurement kind, and recommended defaults.
3. Add or update tests in `pc-app/BrightnessSensor.ConsoleApp.Tests/AppConfigLoaderTests.cs`.
