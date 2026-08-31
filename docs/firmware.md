# Firmware

The active firmware is an ESP-IDF project for Waveshare ESP32-C6 1.47-inch LCD boards with a KY-018 analog light sensor. It supports the `ESP32-C6-LCD-1.47` with `ST7789` and the `ESP32-C6-Touch-LCD-1.47` with `JD9853`.

## Requirements

- ESP-IDF 6.x.
- A Waveshare `ESP32-C6-LCD-1.47` (`ST7789`) or `ESP32-C6-Touch-LCD-1.47` (`JD9853`).
- A KY-018 connected to `3V3`, `GND`, and `GPIO4` (`AO`/`S`) by default.

Do not power the KY-018 from `5V`. Do not use `GPIO0` for its analog signal because it can interfere with normal ESP32-C6 startup.

## Display Variants

`APP_DISPLAY_TYPE` selects the board/display pin profile and matching sprite palette:

| Value | Display | Build directory | Panel color inversion | Notes |
| --- | --- | --- | --- | --- |
| `1` | `ST7789` | `build/` | Enabled | Source-level default when no override is supplied |
| `2` | `JD9853` | `build_touch/` | Disabled | Selected explicitly with `-D APP_DISPLAY_TYPE=2` |

Keep the variants in separate build directories so configuring one does not overwrite the other.

## Build And Flash

Run commands from `firmware/firmware_esp32c6/` in an ESP-IDF PowerShell.

### ST7789

```powershell
idf.py set-target esp32c6
idf.py build
idf.py -p COM5 flash monitor
```

### JD9853

```powershell
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 build
idf.py -B build_touch -D APP_DISPLAY_TYPE=2 -p COM5 flash monitor
```

Replace `COM5` with the board's COM port. Close `pc-app` and other serial monitors before flashing if the port is busy.

## Flash A Prebuilt Merged Binary

A merged image is written at offset `0x0`:

```powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash 0x0 brightness_sensor_esp32c6_merged.bin
```

Reconnect the board or press `RST` after flashing.

## Configuration

The main source-level settings are in:

```text
firmware/firmware_esp32c6/main/app_config.h
```

Important settings include:

- `APP_PROTOCOL_ID` (`lumabloom`); changing it breaks discovery by the current `pc-app` unless the app expectation is changed too.
- `APP_DISPLAY_TYPE`; selects the display profile.
- `APP_READ_INTERVAL_MS` (`200 ms`); controls sensor reads and telemetry publication.
- `APP_DISPLAY_INTERVAL_MS` (`50 ms`); controls UI task updates.
- `APP_ANIMATION_FRAME_INTERVAL_MS` (`150 ms`) and `APP_ANIMATION_HYSTERESIS_PERCENT` (`2%`).
- `APP_KY018_ADC_CHANNEL` and `APP_KY018_ADC_GPIO`; change both when moving the analog signal to another supported ADC pin.
- LCD pins, geometry, orientation, colors, and backlight settings for each display profile.

The non-touch `ST7789` panel requires hardware color inversion (`APP_LCD_INVERT_COLOR=true`) for the intended palette. The touch `JD9853` profile keeps panel color inversion disabled.

Current KY-018 normalization defaults are:

- `APP_KY018_ADC_MIN 200`
- `APP_KY018_ADC_MAX 3200`
- `APP_KY018_INVERT 1`
- `APP_KY018_GAMMA 2.0f`

With inversion enabled, a raw reading near `200` is the bright endpoint and one near `3200` is the dark endpoint.

## Runtime Behavior

The firmware runs independent sensor, serial-command, and display tasks:

- the sensor is sampled every `200 ms`;
- valid raw readings are published immediately as JSONL over USB Serial;
- sensor initialization is retried after failures;
- the LCD refreshes independently, so animation and percentage transitions remain smooth between sensor reads.

At startup the LCD shows `--%` and `ADC ----`. After valid data arrives it shows the locally normalized ambient percentage, a progress bar, `ADC ####`, and the animated flower. A later sensor error shows `ERR` and `ADC ERR` while preserving the last flower frame.

## Animated LCD

The `320x172` screen uses a nine-frame palette-indexed flower animation. Each `160x86` source frame is enlarged 2x with nearest-neighbor pixels. The displayed percentage approaches its target progressively; the flower then moves one frame every `150 ms`, with `2%` hysteresis around frame boundaries.

The master sprite sheet is:

```text
firmware/firmware_esp32c6/assets/flower_animation.png
```

It is an opaque `160x774` RGBA PNG containing nine vertical `160x86` frames. After editing it, regenerate the shared indexed frames and both display palettes from `firmware/firmware_esp32c6/`:

```powershell
python tools\convert_flower_sprite.py assets\flower_animation.png main\flower_sprite_asset.h main\flower_sprite_asset.c
```

The converter uses only the Python standard library and validates the image dimensions, opacity, and palette size. Do not edit the generated `flower_sprite_asset.h` or `flower_sprite_asset.c` manually.

## USB Serial Protocol

The firmware sends newline-delimited JSON (`JSONL`) telemetry over USB Serial at `115200 baud`.

Valid sensor readings produce raw telemetry only:

```json
{"id":"lumabloom","ts":1234567,"raw":1840}
```

The normalized percentage shown on the LCD is local UI state and is not sent to `pc-app`. The current desktop application performs all normalization, smoothing, curve mapping, and user calibration from the `raw` value.

See [`protocol.md`](protocol.md) for the desktop-facing telemetry contract.

## Release Binary

Create a merged binary for the default `ST7789` build:

```powershell
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

The result is written to:

```text
firmware/firmware_esp32c6/build/release/brightness_sensor_esp32c6_merged.bin
```

For a repeatable Codex build and optional flash workflow, see [`skills-for-users.md`](skills-for-users.md).

More implementation detail and separate-bin flashing commands are available in the firmware-specific [`README.md`](../firmware/firmware_esp32c6/README.md).
