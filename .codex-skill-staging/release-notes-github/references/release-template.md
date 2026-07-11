# Release Template

Use this as the starting point for new GitHub releases in this repository.

Replace `<from-tag>`, `<to-tag>`, and artifact names before returning the final text.

```md
## Included In This Release

Ready-to-flash firmware for `Waveshare ESP32-C6-LCD-1.47` plus the Windows companion app package.

Firmware:
- works with the `KY-018` light sensor
- shows the current reading on the LCD
- shows the sensor status on screen
- sends telemetry over `USB Serial`
- stays compatible with the Windows application from this repository

## Release Files

- [luma-bloom-pc-app_<to-tag>_win-x64-portable.zip](https://github.com/mkdsfm/LumaBloom/releases/download/<to-tag>/luma-bloom-pc-app_<to-tag>_win-x64-portable.zip) - Windows app package with `BrightnessSensor.ConsoleApp.exe`, `Tools/esptool.exe`, and the bundled firmware release folder in `Firmware/` for the Update screen

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
& ".\Tools\esptool.exe" --chip esp32c6 --port COM8 --baud 460800 write-flash 0x0 .\Firmware\luma_bloom_esp32c6_<to-tag>_merged.bin
```

If you run the command outside the portable package folder, use the full paths to `Tools\esptool.exe` and the merged firmware file inside `Firmware\`.

If the board is not detected, enter the bootloader mode:
1. hold `BOOT`
2. press and release `RST`
3. release `BOOT`

## Telemetry

Example:

```json
{"id":"lumabloom","ts":1234567,"raw":1872}
```

After inserting the telemetry example, verify against the current sources that the meaning of the `raw` field is described correctly for this release.
```

Before finalizing a real release package, ensure these paths exist when requested:

- `firmware/firmware_esp32c6/build/release/`
- `pc-app/artifacts/single-file/luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`
