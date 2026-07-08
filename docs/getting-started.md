# Getting Started

This guide gets one LumaBloom ESP32-C6 device running with the Windows companion app.

## Requirements

- Windows 10/11.
- .NET SDK 10.0+.
- ESP-IDF 6.x.
- Waveshare `ESP32-C6-LCD-1.47`.
- KY-018 analog light sensor.
- USB data cable.

For enclosure parts, wiring, BOM, and assembly, see [`../hardware/README.md`](../hardware/README.md).

## 1. Flash The Device

Build and flash the ESP32-C6 firmware from [`firmware/firmware_esp32c6/`](../firmware/firmware_esp32c6/).

Detailed commands are in [`firmware.md`](firmware.md).

## 2. Wire The Sensor

Default KY-018 wiring:

| KY-018 | Waveshare ESP32-C6-LCD-1.47 |
| --- | --- |
| `VCC` | `3V3` |
| `GND` | `GND` |
| `AO` / `S` | `GPIO4` |

Full wiring notes are in [`../hardware/WIRING.md`](../hardware/WIRING.md).

## 3. Configure The Windows App

Create `pc-app/appsettings.json` from the ESP32-C6 example:

```powershell
Copy-Item pc-app/appsettings.esp32c6.example.json pc-app/appsettings.json
```

Optional: set `serial.deviceId` if you want the app to discover only one exact device.

Useful config notes:

- `processing.adcMin=200`, `processing.adcMax=3200`, and `processing.invert=true` match the current ESP32-C6 + KY-018 wiring
- `brightness.curve` accepts the main response points as `{ "lightPercent", "brightnessPercent" }`
- `ui.language` accepts `auto`, `en`, `ru`, or `es`

## 4. Run The App

From `pc-app/`:

```powershell
dotnet restore
dotnet run
```

On startup, the app discovers the serial device and starts applying brightness from the active device profile and response curve. The built-in `esp32c6-analog-ky018` flow uses the live raw sensor range directly and does not require startup calibration.

## Expected Result

- The LCD shows the current ambient percentage.
- The app receives JSON lines with `deviceId`, `sensorId`, `ts`, and `raw`.
- Monitor brightness follows the configured brightness curve.

## Next Steps

- Tune brightness behavior in the app settings UI.
- In `Settings -> Response`, you can either edit the main `0/25/50/75/100` curve points directly or use the `Current light` action to say "for the light level right now, I want brightness X%".
- The `Current light` action reads the live sensor value, converts it with the active `adcMin`, `adcMax`, and `invert` settings, rebuilds the whole response curve around that anchor, and saves it immediately.
- The saved curve may contain an extra anchor point such as `16 -> 60` in addition to the usual five visible control points. This is expected and makes the chosen current-light target exact instead of approximate.
- Review [`docs/protocol.md`](protocol.md) when changing telemetry.
- Review [`docs/device-profiles.md`](device-profiles.md) when changing runtime defaults.
