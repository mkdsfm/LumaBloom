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

## 4. Run The App

From `pc-app/`:

```powershell
dotnet restore
dotnet run
```

On startup, the app discovers the serial device, reads raw samples, gets the current monitor brightness, and sends the calibration command to the ESP32-C6.

## Expected Result

- Before calibration, the LCD shows `--%` and telemetry reports `calibrated=false`.
- After calibration, the LCD shows a percentage.
- The app receives JSON lines with `deviceId`, `sensorId`, `ts`, `value`, `raw`, and `calibrated`.
- Monitor brightness follows the configured brightness curve.

## Next Steps

- Tune brightness behavior in the app settings UI.
- Review [`docs/protocol.md`](protocol.md) when changing telemetry or calibration.
- Review [`docs/device-profiles.md`](device-profiles.md) when changing runtime defaults.
