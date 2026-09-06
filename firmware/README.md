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

## Required Release Build Script

Every immediate firmware project directory under `firmware/` must contain its own `build_merged.py`. There is intentionally no shared firmware build implementation: each project owns its toolchain commands, board variants, flash layout, and merge process. The project script may call ESP-IDF, Arduino, PlatformIO, STM32 tools, vendor utilities, or any other required build system.

The script is the stable entrypoint used by release skills and must:

- accept `--tag <version>` and include that tag in every output filename;
- build every hardware/firmware variant supported by that project when no variant filter is supplied;
- create full-device flashable binaries under `build/release/`;
- name each artifact `*_<tag>_merged.bin`;
- delete old release binaries and manifests for the requested tag before creating new artifacts;
- create a `<binary>.manifest.json` sidecar for every merged binary;
- accept `--skip-build` to recreate merged files from existing valid build artifacts;
- accept `--dry-run` for command validation without changing build outputs;
- return a non-zero exit code if any supported variant fails, so a release cannot silently omit firmware.

The Python entrypoint is only a project-local adapter. It does not require the firmware itself to use Python or ESP tooling.

Each manifest uses schema version `1` and contains:

```json
{
  "schemaVersion": 1,
  "version": "2.1.0",
  "fileName": "example_2.1.0_merged.bin",
  "variant": "board-variant",
  "board": "board-id",
  "flashMethod": "vendor-tool",
  "chip": "chip-id",
  "baudRate": 460800,
  "offset": "0x0"
}
```

The Windows app currently accepts only manifests with `flashMethod` set to `esptool`. Other projects still publish their binaries and manifests, but require a matching PC-side flasher before they can be selected in the app.

To build release binaries manually, run every project's script. Release skills discover these entrypoints automatically and fail if a firmware directory does not provide one.

## Documentation

- [`docs/protocol.md`](../docs/protocol.md) — required serial transport and telemetry contract.
- [`docs/sensor-transports-and-monitor-ddc.md`](../docs/sensor-transports-and-monitor-ddc.md) — end-to-end sensor transport and monitor-control architecture.
- [`firmware_esp32c6/README.md`](firmware_esp32c6/README.md) — ESP32-C6 LCD reference implementation.
- [`firmware_esp32c3_supermini/README.md`](firmware_esp32c3_supermini/README.md) — displayless ESP32-C3 Super Mini reference implementation.
