# Release Template

Use this as the starting point for new GitHub releases in this repository.

Replace `<from-tag>`, `<to-tag>`, and artifact names before returning the final text.

```md
## Included In This Release

Ready-to-flash firmware for `Waveshare ESP32-C6-LCD-1.47`.

Firmware:
- works with the `KY-018` light sensor
- shows the current reading on the LCD
- shows the sensor status on screen
- sends telemetry over `USB Serial`
- stays compatible with the Windows application from this repository

## Release Files

- [luma-bloom-pc-app_<to-tag>_win-x64-portable.zip](https://github.com/mkdsfm/LumaBloom/releases/download/<to-tag>/luma-bloom-pc-app_<to-tag>_win-x64-portable.zip) - folder with the `exe` file
- [luma_bloom_esp32c6_<to-tag>_merged.bin](https://github.com/mkdsfm/LumaBloom/releases/download/<to-tag>/luma_bloom_esp32c6_<to-tag>_merged.bin) - firmware binary for flashing the device

## What Changed

- list the key changes in the `<from-tag> -> <to-tag>` range

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
& "C:\Espressif\tools\python\v6.0\venv\Scripts\esptool.exe" --chip esp32c6 --port COM8 --baud 460800 write-flash 0x0 luma_bloom_esp32c6_<to-tag>_merged.bin
```

If you run the command outside the artifact folder, use the full path to `luma_bloom_esp32c6_<to-tag>_merged.bin`.

If the board is not detected, enter the bootloader mode:
1. hold `BOOT`
2. press and release `RST`
3. release `BOOT`

## Telemetry

Example:

```json
{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":1872}
```

After inserting the telemetry example, verify against the current sources that the meaning of the `value` field is described correctly for this release.
```

Before finalizing a real release package, ensure these files exist when requested:

- `firmware/firmware_esp32c6/build/release/luma_bloom_esp32c6_<to-tag>_merged.bin`
- `pc-app/artifacts/portable/luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`
