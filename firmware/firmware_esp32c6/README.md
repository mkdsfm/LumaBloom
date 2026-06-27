# brightness_sensor_esp32c6

`ESP-IDF` firmware for the `Waveshare ESP32-C6-LCD-1.47` board.

## What the Firmware Does

- uses the built-in `1.47"` LCD driven by `ST7789`;
- reads the raw `ADC` value from a `KY-018` photoresistor module;
- shows status and the current reading on the horizontal LCD;
- exchanges JSONL messages with `pc-app` over `USB Serial`;
- stays compatible with the Windows application in `pc-app/`.

Telemetry format:

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":123456,"value":742,"raw":1840,"calibrated":true}`

For this firmware, the `value` field contains a calibrated normalized reading in the `0..1000` range.
Before startup calibration arrives from `pc-app`, the firmware publishes `value=0` and `calibrated=false`.
The built-in LCD also shows `UNCAL` until calibration completes.

Calibration command from `pc-app`:

`{"type":"calibrate","screenBrightnessPercent":65,"sensorAverageRaw":1840}`

Calibration response from the firmware:

`{"type":"calibrationResult","success":true,"calibrated":true,"normalizedOffset":0.153846,"message":"calibration applied"}`

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

- `NORM 1000`
- `RAW ...`
- `UNCAL` before startup calibration, then a numeric normalized value after calibration
- a status line like `STATUS ...`
- `deviceId`

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

- `serial.deviceId` must match `APP_DEVICE_ID` in `main/app_config.h`;
- the default is `esp32c6-01`;
- `baudRate` must be `115200`.
- `pc-app` must complete startup calibration before telemetry becomes usable for brightness control.

## Project Settings

The main constants are defined in:

- `main/app_config.h`

You can change:

- `APP_DEVICE_ID`
- `APP_SENSOR_ID`
- refresh intervals
- `APP_KY018_ADC_CHANNEL`
- `APP_KY018_ADC_GPIO`
- LCD pins and dimensions

If you change `APP_DEVICE_ID`, do not forget to update `serial.deviceId` in `pc-app/appsettings.json`.
