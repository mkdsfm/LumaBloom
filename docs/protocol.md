# Communication Protocol

The firmware sends newline-delimited JSON (`JSONL`) telemetry over USB Serial.

## Rate

- Send interval: `200 ms`
- Port speed: `115200 baud`

## Telemetry Format

`{"id":"lumabloom","ts":1234567,"raw":1840}`

## Telemetry Fields

- `id` (`string`) - protocol identifier; must be `lumabloom` for `pc-app` to treat the COM port as a compatible device
- `ts` (`number`) - milliseconds since device startup
- `raw` (`number`) - raw ADC reading used by `pc-app` for brightness processing, diagnostics, and curve anchoring

## `raw` Semantics

- For both `firmware/firmware_esp32c6/` and `firmware/firmware_esp32c3_supermini/`, `raw` is the only telemetry measurement field on the USB wire contract.
- The on-device LCD still derives a normalized ambient percent from the configured raw range, but that normalized display value is local to firmware UI and is not transmitted to `pc-app`.
- The ESP32-C3 Super Mini track has no display and sends the same native ADC field without firmware-side normalization.

## Message Separator

- Every message ends with a newline (`\n`)
