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

## How To Flash

For `<to-tag>`, the recommended update path is from the Windows companion app itself. Open the packaged BrightnessSensor.ConsoleApp.exe, go to the Update screen, and start the firmware update from there. The portable package already includes Tools/esptool.exe and the bundled firmware file in Firmware\.

If you need a manual fallback, run:

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
