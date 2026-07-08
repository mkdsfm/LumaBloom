# Communication Protocol

The firmware uses USB Serial as a bidirectional JSONL channel.

## Rate

- Send interval: `500 ms`
- Port speed: `115200 baud`

## Telemetry Format

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"raw":1840}`

## Telemetry Fields

- `deviceId` (`string`) - device identifier; used by the PC application to select the hardware profile and to autodiscover the COM port when `serial.deviceId` is set in the config
- `sensorId` (`string`) - sensor identifier
- `ts` (`number`) - milliseconds since device startup
- `raw` (`number`) - raw ADC reading used by `pc-app` for brightness processing, diagnostics, and curve anchoring
## `raw` Semantics

- For `firmware/firmware_esp32c6/`, `raw` is the only telemetry measurement field on the USB wire contract.
- The on-device LCD still derives a normalized ambient percent from the configured raw range, but that normalized display value is local to firmware UI and is not transmitted to `pc-app`.

## Optional Calibration Command Compatibility

`{"type":"calibrate","screenBrightnessPercent":65,"sensorAverageRaw":1840}`

Fields:

- `type` must be `calibrate`
- `screenBrightnessPercent` is the current monitor brightness in `0..100`
- `sensorAverageRaw` is the averaged raw ADC sample collected by `pc-app` when a compatible calibration flow is used

## Calibration Response From ESP32-C6

`{"type":"calibrationResult","success":true,"normalizedOffset":0.000000,"message":"calibration applied"}`

Fields:

- `success` indicates whether the command was accepted
- `normalizedOffset` is a compatibility diagnostics field from the firmware calibration state; current `esp32c6-analog-ky018` app behavior does not require it for normal brightness control
- `message` is a short status string for logs or diagnostics

## Message Separator

- Every message ends with a newline (`\n`)
