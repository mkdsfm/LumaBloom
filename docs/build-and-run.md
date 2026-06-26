# Build and Run

## Hardware Assembly (ESP32-C3 + Light Sensor)

Required components:

- ESP32-C3 board with USB for flashing and power
- KY-018 light sensor module
- Breadboard or another safe mounting method
- Dupont jumper wires, at least 3
- USB cable for connecting the ESP32-C3 to a PC

Wiring requirements:

- `KY-018 VCC` -> `ESP32-C3 3V3` (not `5V`)
- `KY-018 GND` -> `ESP32-C3 GND`
- `KY-018 AO` -> `ESP32-C3 GPIO0` (ADC)
- Only the analog sensor output (`AO`) is used
- A common `GND` between the sensor and the board is required

Signal quality recommendations:

- Use short wires and reliable contacts
- Do not route the signal wire near interference sources such as noisy USB hubs or unshielded power supplies
- Before flashing, verify the assembly against the diagram in `docs/wiring.md`

## Firmware (ESP32-C3, Arduino `.ino`)

Requirements:

- Arduino IDE 2.x
- Installed `esp32` board package from Espressif Systems
- ESP32-C3 connected over USB

Steps:

1. Open `firmware/firmware_esp32c3/firmware_esp32c3.ino` in Arduino IDE.
2. Select the ESP32-C3 board in **Tools -> Board**.
3. Select the device COM port in **Tools -> Port**.
4. Enable `USB CDC On Boot` in the tools menu if your board requires it.
5. Check `kDeviceId`: it should be unique and should later match `serial.deviceId` in the PC application config.
6. Click **Upload** to flash the firmware.
7. Open **Serial Monitor** and set the speed to `115200`.

Expected monitor output: JSON lines with `deviceId`, `sensorId`, `ts`, and `value`.

Detailed instructions, including `arduino-cli` and release binary builds:

- `firmware/firmware_esp32c3/README.md`

## Firmware (ESP32-C6, ESP-IDF, KY-018 + LCD 1.47)

Requirements:

- ESP-IDF 5.x
- Waveshare `ESP32-C6-LCD-1.47` board
- `KY-018` sensor

Default KY-018 wiring in the project:

- `KY-018 VCC` -> `3V3`
- `KY-018 GND` -> `GND`
- `KY-018 AO` -> `GPIO4` (ADC)

If you connected the sensor to a different ADC pin, update the constants in `firmware/firmware_esp32c6/main/app_config.h`.
Using `GPIO0` on `ESP32-C6` is not recommended because it may interfere with normal board startup when the sensor is attached.

Steps:

1. Open an ESP-IDF terminal.
2. Go to `firmware/firmware_esp32c6`.
3. Set the target:

```powershell
idf.py set-target esp32c6
```

4. Open configuration if needed:

```powershell
idf.py menuconfig
```

5. Build the project:

```powershell
idf.py build
```

6. Flash and open the monitor:

```powershell
idf.py -p COMx flash monitor
```

Expected result:

- the LCD shows `deviceId`, `adc`, `value`, and `status`;
- the serial monitor receives JSON lines with `deviceId`, `sensorId`, `ts`, and `value`;
- the Windows application from `pc-app/` can work with the new device using the same contract.

## PC Application (.NET)

Requirements:

- Windows 10/11
- .NET SDK 10.0+

Preparation:

1. Create `pc-app/appsettings.json` from the appropriate example:
   `../appsettings.example.json` for ESP32-C3, or `appsettings.esp32c6.example.json` for ESP32-C6 + KY-018.
2. Optionally set `serial.deviceId` if you want to limit autodiscovery to one specific device. If this field is not set, the app uses the first COM port with valid telemetry.
3. Only add the overrides you actually need, such as brightness limits, a forced `deviceProfile.profileId` for debugging, or selected `processing` / `calibration` fields on top of the built-in profile.

Run from `pc-app/`:

```powershell
dotnet restore
dotnet run
```

The application automatically finds the COM port, reads the first valid messages, selects a built-in hardware profile by `deviceId + sensorId`, logs the effective settings, then computes the target brightness and applies it through WMI to the built-in display.

For the list of built-in profiles and instructions for adding a new one, see `docs/device-profiles.md`.

Important: the implementation in `pc-app/` is Windows-only. Other operating systems would need a separate application that supports the same device communication contract, meaning JSON lines defined by `docs/protocol.md`.
