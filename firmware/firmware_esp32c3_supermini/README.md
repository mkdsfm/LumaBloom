# brightness_sensor_esp32c3_supermini

ESP-IDF firmware for an `ESP32-C3 Super Mini` with a `KY-018` analog light sensor and no display.

The firmware reads the native 12-bit ADC value every `200 ms` and sends the existing LumaBloom JSONL telemetry over the ESP32-C3 built-in USB Serial/JTAG console:

```json
{"id":"lumabloom","ts":1234567,"raw":1840}
```

Normalization, smoothing, curve mapping, and monitor brightness control remain in `pc-app`.

## Supported Hardware

This project targets Super Mini boards whose USB connector is wired to the ESP32-C3 built-in USB Serial/JTAG controller.

| KY-018 | ESP32-C3 Super Mini |
| --- | --- |
| `VCC` / `+` | `3V3` |
| `GND` / `-` | `GND` |
| `AO` / `S` | `GPIO4` (`ADC1_CH4`) |

Use only the analog output and do not power the KY-018 from `5V`. GPIO18 and GPIO19 are reserved for USB D- and D+.

## Build And Flash

Use an ESP-IDF 6.x PowerShell:

```powershell
cd firmware\firmware_esp32c3_supermini
idf.py set-target esp32c3
idf.py build
idf.py -p COM5 flash monitor
```

Replace `COM5` with the board's port. Close `pc-app` and other serial monitors before flashing if the port is busy.

The USB console is selected in `sdkconfig.defaults`. The project does not use light sleep or deep sleep because either can remove the USB serial device.

## Release Binary

Create the full-device merged binary and manifest:

```powershell
python build_merged.py --tag 2.1.0
```

Outputs:

```text
build/release/luma_bloom_esp32c3-supermini_2.1.0_merged.bin
build/release/luma_bloom_esp32c3-supermini_2.1.0_merged.bin.manifest.json
```

Flash the merged binary at offset `0x0`:

```powershell
esptool.py --chip esp32c3 --port COM5 --baud 460800 write-flash 0x0 build\release\luma_bloom_esp32c3-supermini_2.1.0_merged.bin
```

The build adapter also supports `--list`, `--variant esp32c3-supermini`, `--skip-build`, and `--dry-run`.

## PC App Settings

The current built-in baseline (`adcMin=200`, `adcMax=3200`, `invert=true`) is a starting point. KY-018 modules and mounting conditions vary, so observe `raw` in bright and dark conditions and tune `processing.adcMin`, `processing.adcMax`, and `processing.invert` through the app or `appsettings.json`.

No firmware calibration command is required.

## Expected Runtime Behavior

- startup logs identify the ESP32-C3 Super Mini target and GPIO4 sensor input;
- every successful read produces one JSON line;
- ADC errors are logged and never produce invented telemetry;
- after a read failure the ADC handle is released and initialization is retried on the next cycle;
- no display, buttons, Wi-Fi, Bluetooth, or serial command task is started.

## Troubleshooting

- If no COM port appears, confirm that the board exposes built-in USB Serial/JTAG rather than only an unrelated USB-UART bridge.
- If the firmware was configured in a way that disables USB, hold `BOOT` (GPIO9 low), reset the board, and flash a corrected build.
- If `raw` does not react, verify `AO/S -> GPIO4`, `3V3`, common `GND`, and short reliable wiring.
- If flashing reports a busy port, close the Windows app and all serial terminals.
