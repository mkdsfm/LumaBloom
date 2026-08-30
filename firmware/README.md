# Custom Firmware

You can implement compatible firmware for any board, microcontroller, sensor, or framework that can expose a serial port to the Windows application.

## Required Functionality

The firmware must:

- read the ambient-light sensor and provide its raw integer value;
- expose a serial connection at `115200 baud`;
- send one JSON object per line approximately every `200 ms`;
- terminate every message with `\n`;
- identify the protocol with `"id":"lumabloom"`;
- include an integer uptime timestamp in milliseconds in `ts`;
- include the raw sensor reading in `raw`;
- continue sending telemetry while the device is connected.

Example:

```json
{"id":"lumabloom","ts":1234567,"raw":1840}
```

## Sensor Value

`raw` must be a JSON integer that fits in a signed 32-bit value. Send the sensor value in its native, consistent scale; for example, ADC counts in `0..1023` or `0..4095`. Integer lux values are also usable. Do not convert the value to a percentage and do not assume that a specific ADC range is required.

The scale must have a consistent direction: apart from normal sensor noise, `raw` should generally increase or generally decrease as the room gets brighter. Either direction is supported because the Windows application can invert it. Values outside the configured range are clamped.

The current PC defaults are `adcMin=200`, `adcMax=3200`, and `invert=true`, matching the reference ESP32-C6 with KY-018. A firmware implementation for another sensor or board can use a different range.

## PC Processing And Configuration

The Windows application:

1. clamps `raw` to the configured `adcMin..adcMax` range and normalizes it to `0..1`;
2. reverses the normalized value when `invert=true`;
3. applies EMA smoothing and gamma correction;
4. maps the result through the configurable ambient-light-to-monitor-brightness curve;
5. applies brightness limits, hysteresis, and a maximum step size.

The sensor range, direction, smoothing, gamma, brightness curve, brightness limits, hysteresis, and maximum step are configurable in `pc-app` or `appsettings.json`. Therefore, firmware-side normalization, smoothing, calibration commands, and brightness calculation are not required. A display and buttons are also optional.

## Documentation

- [`docs/protocol.md`](../docs/protocol.md) — required serial transport and telemetry contract.
- [`docs/sensor-transports-and-monitor-ddc.md`](../docs/sensor-transports-and-monitor-ddc.md) — end-to-end sensor transport and monitor-control architecture.
- [`firmware_esp32c6/README.md`](firmware_esp32c6/README.md) — optional ESP32-C6 reference implementation.
