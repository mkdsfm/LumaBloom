## Included In This Release

Ready-to-flash firmware for `Waveshare ESP32-C6-LCD-1.47`.

Firmware:
- works with the `KY-018` light sensor
- shows the current reading on the LCD
- shows the sensor status on screen
- sends telemetry over `USB Serial`
- stays compatible with the Windows application from this repository

## Release Files

- [luma-bloom-pc-app_1.3.0_win-x64-portable.zip](https://github.com/mkdsfm/LumaBloom/releases/download/1.3.0/luma-bloom-pc-app_1.3.0_win-x64-portable.zip) - folder with the `exe` file
- [luma_bloom_esp32c6_1.3.0_merged.bin](https://github.com/mkdsfm/LumaBloom/releases/download/1.3.0/luma_bloom_esp32c6_1.3.0_merged.bin) - firmware binary for flashing the device

## What Changed

- updated the Windows dashboard version label from `v1.2.0` to `v1.3.0`
- refreshed the release artifacts and release metadata for the `1.3.0` package
- kept the existing ESP32-C6 calibration flow, normalized `0..1000` telemetry, and Windows companion behavior unchanged

## Supported Hardware

- `Waveshare ESP32-C6-LCD-1.47` board
- `KY-018` sensor

Wiring:
- `VCC` -> `3V3`
- `GND` -> `GND`
- `AO` -> `GPIO4`

> `GPIO0` is no longer recommended for `KY-018` on `ESP32-C6` because it can interfere with normal board startup.

## How To Flash

```powershell
& "C:\Espressif\tools\python\v6.0\venv\Scripts\esptool.exe" --chip esp32c6 --port COM8 --baud 460800 write-flash 0x0 luma_bloom_esp32c6_1.3.0_merged.bin
```

If you run the command outside the artifact folder, use the full path to `luma_bloom_esp32c6_1.3.0_merged.bin`.

If the board is not detected, enter the bootloader mode:
1. hold `BOOT`
2. press and release `RST`
3. release `BOOT`

## Telemetry

Example:

```json
{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":742,"raw":1840,"calibrated":true}
```

In this release, `value` is still the calibrated normalized reading in the `0..1000` range.
