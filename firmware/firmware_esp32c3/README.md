# firmware_esp32c3

Arduino firmware for `ESP32-C3` with an analog `KY-018` ambient light sensor.

Firmware file:

- `firmware_esp32c3.ino`

## What the Firmware Does

- reads the `KY-018` through `ADC` on `GPIO0`;
- sends one measurement to `USB Serial` every `500 ms`;
- stays compatible with the Windows application in `pc-app/`.

Telemetry format:

`{"deviceId":"esp32c3-01","sensorId":"light0","ts":123456,"value":1872}`

For this firmware, the `value` field contains the raw `ADC` reading in the `0..4095` range.

## Wiring

- `KY-018 VCC` -> `3V3`
- `KY-018 GND` -> `GND`
- `KY-018 AO` -> `GPIO0`

Important:

- use only `AO`, not the digital output;
- a common `GND` is required;
- do not supply `5V` to the sensor.

## Quick Flashing with Arduino IDE

1. Open `firmware_esp32c3.ino` in Arduino IDE.
2. Install the `esp32` board package from Espressif Systems.
3. Select the `ESP32-C3` board.
4. Select the device COM port.
5. Enable `USB CDC On Boot` if your board requires it.
6. Click `Upload`.

After flashing, open `Serial Monitor` at `115200`.

## Building with Arduino CLI

Official documentation:

- [arduino-cli compile](https://arduino.github.io/arduino-cli/0.34/commands/arduino-cli_compile/)
- [Arduino CLI configuration](https://docs.arduino.cc/arduino-cli/configuration/)

### Install the Espressif Core

```powershell
arduino-cli core update-index
arduino-cli core install esp32:esp32
```

### Build the Binaries

```powershell
arduino-cli compile --fqbn esp32:esp32:esp32c3 --output-dir firmware_esp32c3_build firmware/firmware_esp32c3
```

After the build, binaries will be in:

- `firmware/firmware_esp32c3_build/`

Usually that folder contains:

- the main `.ino.bin`
- `bootloader.bin`
- `partitions.bin`

### Build and Upload Immediately

```powershell
arduino-cli compile --fqbn esp32:esp32:esp32c3 --upload -p COM5 firmware/firmware_esp32c3
```

Replace `COM5` with your port.

## Release Binaries

For releases, it is convenient to publish:

- the main application binary
- `bootloader.bin`
- `partitions.bin`
- a short flashing instruction

If you build with `arduino-cli --output-dir`, take the files from:

- `firmware/firmware_esp32c3_build/`

## Flashing Prebuilt `.bin` Files with `esptool`

Exact file names depend on the Arduino core version, but a typical `ESP32-C3` layout looks like this:

```powershell
esptool.py --chip esp32c3 --port COM5 --baud 460800 write-flash 0x0 bootloader.bin 0x8000 partitions.bin 0x10000 firmware_esp32c3.ino.bin
```

Before publishing, verify the exact file names in the build output directory.

## Firmware Settings

The main parameters are defined directly in `firmware_esp32c3.ino`:

- `kLightSensorPin`
- `kReadIntervalMs`
- `kDeviceId`
- `kSensorId`

Important:

- `kDeviceId` must match `serial.deviceId` in `pc-app/appsettings.json`;
- the default is `esp32c3-01`;
- the serial speed must remain `115200`.

## Verification After Flashing

Expected output in `Serial Monitor`:

```json
{"deviceId":"esp32c3-01","sensorId":"light0","ts":123456,"value":1872}
```

If there is no output:

- check the COM port;
- check the `115200` baud rate;
- check whether `USB CDC On Boot` must be enabled on your board;
- check power and `KY-018` wiring.
