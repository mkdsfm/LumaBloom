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

Current KY-018 defaults are tuned for the common working range observed on this board:

- `APP_KY018_ADC_MIN 200`
- `APP_KY018_ADC_MAX 3200`
- `APP_KY018_INVERT 1`
- `APP_KY018_GAMMA 2.0f`

The on-device LCD percentage is derived directly from this raw range, so a reading near `200` is treated as bright and a reading near `3200` is treated as dark.

Do not use `GPIO0` for the KY-018 signal; it can interfere with normal board startup.

## Expected Serial Output

Firmware emits newline-delimited JSON with raw sensor telemetry:

```json
{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"raw":1840}
```


## Release Binary

For a merged release binary:

```powershell
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

If using Codex workflows, see [`skills-for-users.md`](skills-for-users.md).
