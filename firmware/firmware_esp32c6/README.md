# brightness_sensor_esp32c6

`ESP-IDF` firmware for the `Waveshare ESP32-C6-LCD-1.47` board.

The project supports multiple LCD configurations, including `ST7789` and
`JD9853` based displays. `ST7789` is the default build target, while the
`JD9853` version can be selected explicitly during compilation.

## What the Firmware Does

-   drives the built-in `1.47"` LCD using the selected board/display
    configuration;
-   reads the raw `ADC` value from a `KY-018` photoresistor module;
-   shows a full-screen animated pixel-art flower plus percentage and
    ADC overlays on the horizontal LCD;
-   exchanges JSONL messages with `pc-app` over `USB Serial`;
-   stays compatible with the Windows application in `pc-app/`.

Telemetry format:

`{"id":"lumabloom","ts":123456,"raw":1840}`

For this firmware, `raw` is the only measurement field sent over USB.
The built-in LCD always shows the current ambient percentage plus the
current `ADC` line. That normalized percentage is derived locally from
the configured raw range and is not mirrored into telemetry.

Optional calibration command compatibility:

`{"type":"calibrate","screenBrightnessPercent":65,"sensorAverageRaw":1840}`

Calibration response from the firmware:

`{"type":"calibrationResult","success":true,"normalizedOffset":0.000000,"message":"calibration applied"}`

## LCD UI

The LCD uses a nine-frame palette-indexed sprite animation. The flower
opens as the displayed ambient-light percentage rises and closes as it
falls.

The raw sensor percentage is used as the target value. The displayed
percentage approaches that target progressively instead of jumping
immediately. Larger differences use larger steps, while movement slows
near the target.

The displayed percentage drives:

-   the numeric percentage;
-   the software-rendered progress bar;
-   the target flower animation frame.

Relevant files:

-   `main/ui_screen.c` - the complete screen renderer and UI state
    update entrypoint
-   `main/display_lcd.c` - low-level LCD primitives and pixel font
    rendering
-   `main/app_config.h` - default board selection and board-specific LCD
    configuration
-   `assets/flower_animation.png` - master sprite sheet with nine
    vertical `160x86` frames
-   `main/flower_sprite_asset.c` - generated indexed frames and
    display-specific RGB565/BGR565 palettes
-   `tools/convert_flower_sprite.py` - deterministic standard-library
    asset converter
-   `tools/README.md` - sprite converter usage

Current screen behavior:

-   each `160x86` source frame is expanded to `320x172` with crisp
    nearest-neighbor 2x pixels;
-   the flower advances toward the current light range one frame every
    `150 ms`;
-   frame boundaries use `2%` hysteresis;
-   percentage and `ADC ####` are drawn in the upper-left with a dark
    pixel outline;
-   startup shows `--%` and `ADC ----`; a read error after valid data
    shows `ERR` and `ADC ERR` while preserving the last frame.

The generated sprite asset contains palettes for both supported display
configurations. `ST7789` uses the BGR565 palette and `JD9853` uses the
RGB565 palette. The required palette is selected at compile time using
`APP_DISPLAY_TYPE`.

Runtime behavior:

-   the screen shows an ambient-light percentage derived from the raw
    `KY-018` range;
-   UI transitions are smoothed locally and do not change the raw
    telemetry value;
-   the raw ADC line keeps updating independently of any compatibility
    calibration command.

After editing the master PNG, regenerate the C asset once from this
directory:

``` powershell
python tools\convert_flower_sprite.py assets\flower_animation.png main\flower_sprite_asset.h main\flower_sprite_asset.c
```

The generated asset contains both display palettes, so `--display` is
not required and the converter does not need to be rerun when switching
between `ST7789` and `JD9853`.

See `tools/README.md` for converter details.

## Building the Firmware

### Default ST7789 Build

`ST7789` is the default board/display configuration.

Build normally:

``` powershell
idf.py build
```

The output is created in:

``` text
build/
```

### JD9853 Build

To build the `JD9853` version without changing the default
configuration:

``` powershell
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 build
```

The output is created separately in:

``` text
build_touch/
```

This keeps the normal `ST7789` build in `build/` and the `JD9853` build
in `build_touch/`.

## Building and Flashing from ESP-IDF

### ST7789

Build and flash the default `ST7789` version:

``` powershell
cd firmware\firmware_esp32c6
idf.py set-target esp32c6
idf.py build
idf.py -p COM5 flash monitor
```

Replace `COM5` with the correct serial port.

### JD9853

Build the `JD9853` version into the separate `build_touch/` directory:

``` powershell
cd firmware\firmware_esp32c6
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 build
```

Flash the `JD9853` build:

``` powershell
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 -p COM5 flash monitor
```

Replace `COM5` with the correct serial port.

## Quick Flashing with a Prebuilt Binary

If you already have a merged binary, flashing a single file is the
simplest option.

Expected file name:

-   `brightness_sensor_esp32c6_merged.bin`
-   skill-based release example:
    `brightness_sensor_esp32c6_calibrated.bin`

Flashing command:

``` powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash 0x0 brightness_sensor_esp32c6_merged.bin
```

Replace `COM5` with your port.

After flashing, reconnect the board or press `RST`.

## Building a Merged Binary from Sources

For the default `ST7789` build, open `ESP-IDF PowerShell` and run:

``` powershell
cd firmware\firmware_esp32c6
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

The merged binary will be created here:

-   `build/release/brightness_sensor_esp32c6_merged.bin`

For the Codex skill workflow that creates a readable release filename
and can flash the device automatically, see
[../../docs/skills-for-users.md](../../docs/skills-for-users.md).

## Flashing Separate `.bin` Files

### ST7789

After the default `idf.py build`, the standard artifacts are:

-   `build/bootloader/bootloader.bin`
-   `build/partition_table/partition-table.bin`
-   `build/brightness_sensor_esp32c6.bin`

Flash them with:

``` powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash --flash-mode dio --flash-freq 80m --flash-size 2MB 0x0 build\bootloader\bootloader.bin 0x8000 build\partition_table\partition-table.bin 0x10000 build\brightness_sensor_esp32c6.bin
```

### JD9853

After:

``` powershell
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 build
```

the corresponding artifacts are:

-   `build_touch/bootloader/bootloader.bin`
-   `build_touch/partition_table/partition-table.bin`
-   `build_touch/brightness_sensor_esp32c6.bin`

Flash them with:

``` powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash --flash-mode dio --flash-freq 80m --flash-size 2MB 0x0 build_touch\bootloader\bootloader.bin 0x8000 build_touch\partition_table\partition-table.bin 0x10000 build_touch\brightness_sensor_esp32c6.bin
```

Replace `COM5` with the correct serial port.

## Hardware Wiring

### Onboard LCD

LCD pins, controller type, offsets, orientation, colors, and related
hardware parameters are selected through:

-   `main/app_config.h`

Supported display configurations:

-   `ST7789` - default configuration (`APP_DISPLAY_TYPE=1`)
-   `JD9853` - optional configuration (`APP_DISPLAY_TYPE=2`)

When `APP_DISPLAY_TYPE` is not explicitly defined, the firmware uses
`ST7789`.

Keep board-specific GPIO and panel settings in the corresponding
configuration profile rather than hard-coding them in UI code.

### KY-018

Default wiring:

-   `VCC` -\> `3V3`
-   `GND` -\> `GND`
-   `AO` -\> `GPIO4`

Important:

-   only the analog output `AO` is used;
-   a common `GND` is required;
-   do not supply `5V` to the sensor;
-   for `ESP32-C6`, do not use `GPIO0` as the main `KY-018` pin because
    it may interfere with normal board startup.

If the sensor does not provide valid readings:

-   check that `AO` is connected specifically to `GPIO4`;
-   check `3V3` power and common `GND`;
-   check the contact quality on the breadboard;
-   if the module is labeled `S`, `+`, `-`, then `S` is the analog
    signal (`AO`).

## Expected Behavior After Startup

On the screen:

-   a numeric ambient-light percentage
-   `ADC ####`

In the monitor:

-   `ESP-IDF` startup logs;
-   the message `LCD ready`;
-   after successful sensor initialization, a line similar to:

`KY-018 ready on ADC unit 1, channel 4, gpio=4`

If initialization or reading fails, the firmware logs
`sensor_ky018_* failed`.

## Connecting to the Windows Application

For `pc-app`, use this example:

-   `pc-app/appsettings.esp32c6.example.json`

Important:

-   telemetry must keep `id="lumabloom"` so `pc-app` recognizes the
    device as compatible;
-   `baudRate` must be `115200`;
-   `pc-app` can use telemetry immediately from the raw sensor field
    without startup calibration.

## Project Settings

The main constants are defined in:

-   `main/app_config.h`

You can change:

-   `APP_PROTOCOL_ID`
-   `APP_DISPLAY_TYPE`
-   refresh intervals
-   `APP_KY018_ADC_CHANNEL`
-   `APP_KY018_ADC_GPIO`
-   `APP_KY018_ADC_MIN`
-   `APP_KY018_ADC_MAX`
-   `APP_KY018_GAMMA`
-   LCD pins and dimensions

Display type values:

-   `APP_DISPLAY_ST7789 = 1`
-   `APP_DISPLAY_JD9853 = 2`

If `APP_DISPLAY_TYPE` is not supplied by the build system, `ST7789` is
used by default.

Current defaults for the built-in KY-018 path are tuned to a practical
raw range on the ESP32-C6 board:

-   `APP_KY018_ADC_MIN=200`
-   `APP_KY018_ADC_MAX=3200`
-   `APP_KY018_INVERT=1`
-   `APP_KY018_GAMMA=2.0f`

If you change `APP_PROTOCOL_ID`, update the `pc-app` protocol
expectation to match; the current desktop app expects `lumabloom`.
