# Communication Protocol

The firmware sends one JSON line per measurement over USB Serial (JSONL).

## Rate

- Send interval: `500 ms`
- Port speed: `115200 baud`

## Message Format

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":1234567,"value":1872}`

## Fields

- `deviceId` (`string`) - device identifier; used by the PC application to select the hardware profile and to autodiscover the COM port when `serial.deviceId` is set in the config
- `sensorId` (`string`) - sensor identifier
- `ts` (`number`) - milliseconds since device startup
- `value` (`number`) - numeric measurement value

## `value` Semantics

- For `firmware/firmware_esp32c3/firmware_esp32c3.ino`, `value` contains the raw ADC reading in the `0..4095` range.
- For `firmware/firmware_esp32c6/`, `value` contains the raw ADC reading in the `0..4095` range.

## Message Separator

- Every message ends with a newline (`\n`)
