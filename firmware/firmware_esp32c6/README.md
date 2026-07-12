# brightness_sensor_esp32c6

`ESP-IDF` firmware for the `Waveshare ESP32-C6-LCD-1.47` board.

## What the Firmware Does

- uses the built-in `1.47"` LCD driven by `ST7789`;
- reads the raw `ADC` value from a `KY-018` photoresistor module;
- shows a full-screen animated pixel-art flower plus percentage and ADC overlays on the horizontal LCD;
- exchanges JSONL messages with `pc-app` over `USB Serial`;
- stays compatible with the Windows application in `pc-app/`.

Telemetry format:

`{"id":"lumabloom","ts":123456,"raw":1840}`

For this firmware, `raw` is the only measurement field sent over USB.
The built-in LCD always shows the current ambient percentage plus the current `ADC` line.
That normalized percentage is derived locally from the configured raw range and is not mirrored into telemetry.

Optional calibration command compatibility:

`{"type":"calibrate","screenBrightnessPercent":65,"sensorAverageRaw":1840}`

Calibration response from the firmware:

`{"type":"calibrationResult","success":true,"normalizedOffset":0.000000,"message":"calibration applied"}`

This compatibility command remains available for manual or legacy flows, but the Windows app now works directly from `raw` telemetry and does not require startup calibration.

## LCD UI

The LCD uses a nine-frame palette-indexed sprite animation. The flower opens as the locally normalized ambient-light percentage rises and closes as it falls.

Relevant files:

- `main/ui_screen.c` - the complete screen renderer and UI state update entrypoint
- `main/display_lcd.c` - low-level LCD primitives and pixel font rendering
- `assets/flower_animation.png` - master sprite sheet with nine vertical `160x86` frames
- `main/flower_sprite_asset.c` - generated indexed frames and shared BGR565 panel palette
- `tools/convert_flower_sprite.py` - deterministic standard-library asset converter

Current screen behavior:

- each `160x86` source frame is expanded to `320x172` with crisp nearest-neighbor 2x pixels;
- the flower advances toward the current light range one frame every `150 ms`;
- frame boundaries use `2%` hysteresis;
- percentage and `ADC ####` are drawn in the upper-left with a dark pixel outline;
- startup shows `--%` and `ADC ----`; a read error after valid data shows `ERR` and `ADC ERR` while preserving the last frame.

Palette values use the panel's configured BGR565 channel order. The LCD driver swaps each 16-bit value to the MSB-first byte order required by the SPI transfer.

Runtime behavior:

- the screen shows a direct ambient-light percentage derived from the raw `KY-018` range;
- the raw ADC line keeps updating independently of any compatibility calibration command.

After editing the master PNG, regenerate the C asset from this directory:

```powershell
python tools/convert_flower_sprite.py assets/flower_animation.png main/flower_sprite_asset.h main/flower_sprite_asset.c
```

## Quick Flashing with a Prebuilt Binary

If you already have a merged binary, flashing a single file is the simplest option.

Expected file name:

- `brightness_sensor_esp32c6_merged.bin`
- skill-based release example: `brightness_sensor_esp32c6_calibrated.bin`

Flashing command:

```powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash 0x0 brightness_sensor_esp32c6_merged.bin
```

Replace `COM5` with your port.

After flashing, reconnect the board or press `RST`.

## Building a Merged Binary from Sources

Open `ESP-IDF PowerShell` and run:

```powershell
cd firmware\firmware_esp32c6
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

The merged binary will be created here:

- `build/release/brightness_sensor_esp32c6_merged.bin`

For the Codex skill workflow that creates a readable release filename and can flash the device automatically, see [../../docs/skills-for-users.md](../../docs/skills-for-users.md).

## Flashing Separate `.bin` Files

After `idf.py build`, the standard artifacts are:

- `build/bootloader/bootloader.bin`
- `build/partition_table/partition-table.bin`
- `build/brightness_sensor_esp32c6.bin`

Flashing command:

```powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash --flash-mode dio --flash-freq 80m --flash-size 2MB 0x0 build\bootloader\bootloader.bin 0x8000 build\partition_table\partition-table.bin 0x10000 build\brightness_sensor_esp32c6.bin
```

## Building and Flashing from ESP-IDF

If you build the project locally:

```powershell
cd firmware\firmware_esp32c6
idf.py set-target esp32c6
idf.py build
idf.py -p COM5 flash monitor
```

## Hardware Wiring

### Onboard LCD

The project uses the built-in Waveshare LCD with these pins:

- `MOSI`: `GPIO6`
- `SCLK`: `GPIO7`
- `LCD_CS`: `GPIO14`
- `LCD_DC`: `GPIO15`
- `LCD_RST`: `GPIO21`
- `LCD_BL`: `GPIO22`

Display controller: `ST7789`.

### KY-018

Default wiring:

- `VCC` -> `3V3`
- `GND` -> `GND`
- `AO` -> `GPIO4`

Important:

- only the analog output `AO` is used;
- a common `GND` is required;
- do not supply `5V` to the sensor;
- for `ESP32-C6`, do not use `GPIO0` as the main `KY-018` pin because it may interfere with normal board startup.

If the sensor does not provide valid readings:

- check that `AO` is connected specifically to `GPIO4`;
- check `3V3` power and common `GND`;
- check the contact quality on the breadboard;
- if the module is labeled `S`, `+`, `-`, then `S` is the analog signal (`AO`).

## Expected Behavior After Startup

On the screen:

- a numeric ambient-light percentage
- `ADC ####`

In the monitor:

- `ESP-IDF` startup logs;
- the message `LCD ready`;
- after successful sensor initialization, a line similar to:

`KY-018 ready on ADC unit 1, channel 4, gpio=4`

If initialization or reading fails, the firmware logs `sensor_ky018_* failed`.

## Connecting to the Windows Application

For `pc-app`, use this example:

- `pc-app/appsettings.esp32c6.example.json`

Important:

- telemetry must keep `id="lumabloom"` so `pc-app` recognizes the device as compatible;
- `baudRate` must be `115200`.
- `pc-app` can use telemetry immediately from the raw sensor field without startup calibration.

## Project Settings

The main constants are defined in:

- `main/app_config.h`

You can change:

- `APP_PROTOCOL_ID`
- refresh intervals
- `APP_KY018_ADC_CHANNEL`
- `APP_KY018_ADC_GPIO`
- `APP_KY018_ADC_MIN`
- `APP_KY018_ADC_MAX`
- `APP_KY018_GAMMA`
- LCD pins and dimensions

Current defaults for the built-in KY-018 path are tuned to a practical raw range on the ESP32-C6 board:

- `APP_KY018_ADC_MIN=200`
- `APP_KY018_ADC_MAX=3200`
- `APP_KY018_INVERT=1`
- `APP_KY018_GAMMA=2.0f`

If you change `APP_PROTOCOL_ID`, update the `pc-app` protocol expectation to match; the current desktop app expects `lumabloom`.
