# Communication Protocol

The firmware uses USB Serial as a bidirectional JSONL channel.

## Rate

- Send interval: `500 ms`
- Port speed: `115200 baud`

## Telemetry Format

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":742,"raw":1840,"calibrated":true}`

## Telemetry Fields

- `deviceId` (`string`) - device identifier; used by the PC application to select the hardware profile and to autodiscover the COM port when `serial.deviceId` is set in the config
- `sensorId` (`string`) - sensor identifier
- `ts` (`number`) - milliseconds since device startup
- `value` (`number`) - numeric measurement value
- `raw` (`number`, optional) - raw ADC reading used by `pc-app` for startup calibration and diagnostics
- `calibrated` (`boolean`) - whether the device has already received runtime calibration from `pc-app`

## `value` Semantics

- For `firmware/firmware_esp32c6/`, `value` contains the calibrated normalized sensor reading in the `0..1000` range.
- Before `firmware/firmware_esp32c6/` is calibrated, it publishes `value=0` and `calibrated=false`.

## Calibration Command From `pc-app` To ESP32-C6

`{"type":"calibrate","screenBrightnessPercent":65,"sensorAverageRaw":1840}`

Fields:

- `type` must be `calibrate`
- `screenBrightnessPercent` is the current monitor brightness in `0..100`
- `sensorAverageRaw` is the averaged raw ADC sample collected by `pc-app` during startup

## Calibration Response From ESP32-C6

`{"type":"calibrationResult","success":true,"calibrated":true,"normalizedOffset":0.153846,"message":"calibration applied"}`

Fields:

- `success` indicates whether the command was accepted
- `calibrated` indicates whether the device is now calibrated
- `normalizedOffset` is the runtime offset applied before the `0..1000` output mapping
- `message` is a short status string for logs or diagnostics

## Message Separator

- Every message ends with a newline (`\n`)
