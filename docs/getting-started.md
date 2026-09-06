# Getting Started

This guide gets one supported LumaBloom ESP32 device running with the Windows companion app.

## Requirements

- Windows 10/11.
- .NET SDK 10.0+.
- ESP-IDF 6.x.
- Waveshare `ESP32-C6-LCD-1.47` or an ESP32-C3 Super Mini with built-in USB Serial/JTAG.
- KY-018 analog light sensor.
- USB data cable.

For enclosure parts, wiring, BOM, and assembly, see [`../hardware/README.md`](../hardware/README.md).

## 1. Flash The Device

Choose the project for your board:

- ESP32-C6 with LCD: [`firmware/firmware_esp32c6/`](../firmware/firmware_esp32c6/)
- ESP32-C3 Super Mini without display: [`firmware/firmware_esp32c3_supermini/`](../firmware/firmware_esp32c3_supermini/)

Detailed commands are in [`firmware.md`](firmware.md).

The packaged Windows app can also flash bundled firmware. Open `Update`, switch to the `Device firmware` inner tab, select the required firmware version from the `.bin` files included in the package's `Firmware` folder, then select the target port. Each selectable binary must have a valid `<binary>.manifest.json`; the app currently supports `esptool` manifests. The firmware-port dropdown selects the automatically detected LumaBloom port by default; open it to rescan all current COM ports and choose another port when needed. Windows device descriptions help distinguish Bluetooth, USB serial, and Espressif/ESP32 ports. Manual firmware and port selections are temporary and do not change the port used for normal telemetry discovery.

## 2. Wire The Sensor

Default KY-018 wiring:

| KY-018 | ESP32-C6 LCD | ESP32-C3 Super Mini |
| --- | --- | --- |
| `VCC` | `3V3` | `3V3` |
| `GND` | `GND` | `GND` |
| `AO` / `S` | `GPIO4` | `GPIO4` (`ADC1_CH4`) |

Full wiring notes are in [`../hardware/WIRING.md`](../hardware/WIRING.md).

## 3. Configure The Windows App

Create `pc-app/appsettings.json` from the full analog example:

```powershell
Copy-Item pc-app/appsettings.esp32c6.example.json pc-app/appsettings.json
```

Useful config notes:

- `processing.adcMin=200`, `processing.adcMax=3200`, and `processing.invert=true` match the ESP32-C6 baseline and are a starting point for ESP32-C3; tune them from actual bright and dark readings
- `brightness.curve` accepts the main response points as `{ "lightPercent", "brightnessPercent" }`
- `ui.language` accepts `auto`, `en`, `ru`, or `es`

## 4. Run The App

From `pc-app/`:

```powershell
dotnet restore
dotnet run
```

On startup, the app probes available COM ports for the first valid LumaBloom telemetry stream, accepts the first port that emits `{"id":"lumabloom","ts":...,"raw":...}`, and starts applying brightness from the resolved app settings and response curve.

Firmware flashing remains available from `Update` while the app is waiting for valid telemetry, provided Windows exposes the target COM port and the package contains both `Tools/esptool.exe` and a bundled firmware file.

## Expected Result

- On ESP32-C6, the LCD shows the current ambient percentage; ESP32-C3 intentionally has no display.
- The app receives JSON lines with `id`, `ts`, and `raw`.
- Monitor brightness follows the configured brightness curve.

## Next Steps

- Tune brightness behavior in the app settings UI.
- In `Settings -> Response`, you can either edit the main `0/25/50/75/100` curve points directly or use the `Current light` action to say "for the light level right now, I want brightness X%".
- The `Current light` action reads the live sensor value, converts it with the active `adcMin`, `adcMax`, and `invert` settings, rebuilds the whole response curve around that anchor, and saves it immediately.
- The saved curve may contain an extra anchor point such as `16 -> 60` in addition to the usual five visible control points. This is expected and makes the chosen current-light target exact instead of approximate.
- Review [`docs/protocol.md`](protocol.md) when changing telemetry.
- Review [`docs/device-profiles.md`](device-profiles.md) when changing runtime defaults or the single-device settings model.
