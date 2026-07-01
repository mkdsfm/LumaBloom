# Firmware

The active firmware target is Waveshare `ESP32-C6-LCD-1.47` with a KY-018 analog light sensor.

## Requirements

- ESP-IDF 6.x.
- Waveshare `ESP32-C6-LCD-1.47`.
- KY-018 wired to `GPIO4` by default.

## Build And Flash

From `firmware/firmware_esp32c6/` in an ESP-IDF terminal:

```powershell
idf.py set-target esp32c6
idf.py build
idf.py -p COMx flash monitor
```

Replace `COMx` with the device COM port.

## Configuration

Main firmware constants live in:

```text
firmware/firmware_esp32c6/main/app_config.h
```

If the KY-018 sensor is connected to a different ADC pin, update `APP_KY018_ADC_CHANNEL` and the related `APP_KY018_ADC_GPIO`.

Do not use `GPIO0` for the KY-018 signal; it can interfere with normal board startup.

## Expected Serial Output

After calibration, firmware emits newline-delimited JSON:

```json
{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":742,"raw":1840,"calibrated":true}
```

Before calibration, the device publishes `value=0` and `calibrated=false`.

## Release Binary

For a merged release binary:

```powershell
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

If using Codex workflows, see [`skills-for-users.md`](skills-for-users.md).
