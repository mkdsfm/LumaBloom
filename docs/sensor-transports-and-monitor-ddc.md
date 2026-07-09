# Reading Sensor Data over USB and Controlling Monitor Brightness

- [English](sensor-transports-and-monitor-ddc.md)
- [Русский](sensor-transports-and-monitor-ddc.ru.md)

This document explains two practical parts of the current system:

1. how sensor readings are read and interpreted over `USB`;
2. how computed brightness is sent to the monitor through `DDC/CI`, and how to check whether the monitor supports it.

## Current State

This repository currently implements:

- sensor telemetry over `USB Serial`;
- newline-delimited `JSON` messages such as `{"id":"lumabloom","ts":1234567,"raw":1840}`;
- `raw` as the only sensor value used by the desktop brightness pipeline;
- monitor brightness control in `Windows` through:
  - `WMI` for built-in displays;
  - `DDC/CI` for external monitors.

This repository does not currently implement:

- direct monitor control from `ESP32-C6` firmware over physical `I2C`.

Important: in this project, `DDC/CI` is used from the Windows side through monitor APIs. That is different from directly driving monitor `I2C` lines from `ESP32`.

## 1. Reading and Interpreting Sensor Values

## 1.1. Source of Truth

For the current `ESP32-C6` device, the source of truth is the `raw` field in telemetry:

```json
{"id":"lumabloom","ts":1234567,"raw":1840}
```

Where:

- `id` must be `lumabloom`;
- `ts` is milliseconds since device startup;
- `raw` is the raw `ADC` reading.

`raw` is currently the only measurement used by `pc-app` to compute brightness.

## 1.2. Transport in the Current Project

### `USB`

In the current project, `USB` is the main transport between the device and `pc-app`.

In practice this means:

- the device appears in Windows as a `COM` port;
- `pc-app` scans available `COM` ports;
- the first matching port is the one that emits valid JSONL messages with `id="lumabloom"` and a `raw` field.

Current wire-contract parameters:

- speed: `115200 baud`;
- message separator: `\n`;
- format: one JSON object per line;
- typical send interval: about `500 ms`.

The current `USB Serial` wire contract behaves like a sequential text stream:

- there is a byte stream;
- messages are framed by `\n`;
- each completed line must be a standalone JSON message.

## 1.3. Validating an Incoming Message

A message is usable only if all of the following are true:

- the line was fully read up to `\n`;
- the line is valid `JSON`;
- the root element is an object;
- there is a string field `id`;
- `id == "lumabloom"`;
- there is a numeric field `ts`;
- there is a numeric field `raw`.

If any of these checks fails, the message must not be used for brightness computation.

## 1.4. Interpreting `raw`

In the current implementation, `raw` is processed with these working values:

- `adcMin = 200`
- `adcMax = 3200`
- `invert = true`

In user configuration, the same parameters are exposed as:

- `processing.adcMin`
- `processing.adcMax`
- `processing.invert`

Current built-in defaults in `pc-app`:

- `processing.adcMin = 200`
- `processing.adcMax = 3200`
- `processing.invert = true`

This means:

- `200` is the lower bound of the working range and, in the current wiring, corresponds to the brightest scene;
- `3200` is the upper bound of the working range and, in the current wiring, corresponds to the darkest scene;
- if `raw` goes below `200`, the application still treats it as `200`;
- if `raw` goes above `3200`, the application still treats it as `3200`;
- after normalization, the value is inverted because in the current wiring more ambient light produces a lower `raw`.

### How the Parameters Affect Interpretation

#### `processing.adcMin`

This is the lower bound of the working range.

If `raw < adcMin`, the application treats the value as `adcMin`.

Practical effect:

- a smaller `adcMin` expands the usable range downward;
- a larger `adcMin` makes the system reach the "bright enough" region earlier;
- if `adcMin` is too large, part of the useful bright-light range gets compressed.

#### `processing.adcMax`

This is the upper bound of the working range.

If `raw > adcMax`, the application treats the value as `adcMax`.

Practical effect:

- a larger `adcMax` expands the usable range upward;
- a smaller `adcMax` makes the system reach the "dark enough" region earlier;
- if `adcMax` is too small, different dark scenes may start looking almost identical to the algorithm.

#### `processing.invert`

This flag defines how the direction of `raw` growth is interpreted.

If:

```text
invert = false
```

then a larger `raw` means more light after normalization.

If:

```text
invert = true
```

then after normalization the following is applied:

```text
normalized = 1.0 - normalized
```

and more ambient light corresponds to a smaller `raw`.

For the current `KY-018` wiring, the project uses:

```text
invert = true
```

### Conversion Formula

First, the input value is clamped to the working range:

```text
clamped = clamp(raw, adcMin, adcMax)
```

Then it is normalized:

```text
normalized = (clamped - adcMin) / (adcMax - adcMin)
```

If `invert = true`, one more step is applied:

```text
normalized = 1.0 - normalized
```

Intuitively for the current hardware:

- `raw` near `200` means brighter ambient light;
- `raw` near `3200` means a darker scene.

### Choosing a Good Range

If the range is chosen well:

- real light changes occupy a meaningful part of the `0.0..1.0` interval;
- the algorithm does not hit `0` or `1` too often;
- the brightness curve behaves predictably.

If the range is chosen poorly:

- too many values get stuck at the lower or upper boundary;
- normalized output loses sensitivity;
- auto-brightness becomes either too sluggish or too abrupt in a narrow range.

## 1.5. What Happens after Normalization

After normalization, `pc-app` sends the value through the brightness pipeline.

Parameters used in this stage:

- `adcMin`, `adcMax`, `invert` for input normalization;
- `emaAlpha` for exponential smoothing;
- `gamma` for optional gamma correction after smoothing;
- `minPercent`, `maxPercent` for the final allowed brightness range;
- `brightness.curve` for mapping `lightPercent -> brightnessPercent`;
- `hysteresisPercent` for the minimum brightness change worth applying;
- `maxBrightnessStepPercent` for the maximum change per cycle.

Computation order:

1. `raw` becomes `normalized` in the range `0.0..1.0`;
2. `normalized` is smoothed with `EMA`;
3. `gamma` is optionally applied to the smoothed value;
4. the result is converted to `requestedBrightness`;
5. `requestedBrightness` is clamped to `minPercent..maxPercent`;
6. if the change is too small, `hysteresis` blocks it;
7. if the change is too large, the step is limited by `maxBrightnessStepPercent`.

### `EMA` Formula

If this is not the first value:

```text
ema = (emaAlpha * normalized) + ((1.0 - emaAlpha) * previousEma)
```

If this is the first value:

```text
ema = normalized
```

History enters the pipeline here:

- in the first cycle, `ema` is just the current `normalized`;
- in each next cycle, the current `normalized` is mixed with the previous `ema`;
- `previousEma` is the smoothed value from the previous step, not the previous `raw`.

Written across several cycles:

```text
cycle 1:
ema1 = normalized1

cycle 2:
ema2 = (emaAlpha * normalized2) + ((1.0 - emaAlpha) * ema1)

cycle 3:
ema3 = (emaAlpha * normalized3) + ((1.0 - emaAlpha) * ema2)
```

So the current `ema` always contains some contribution from earlier measurements, with older samples contributing less over time.

A larger `emaAlpha` makes the system react faster.
A smaller `emaAlpha` makes it smoother but slower.

### Gamma Correction Formula

If `gamma` is set:

```text
effectiveValue = pow(ema, gamma)
```

If `gamma` is not set:

```text
effectiveValue = ema
```

In practice:

- `gamma > 1` changes sensitivity more strongly across the range;
- `gamma = 1` is effectively linear;
- without `gamma`, the smoothed value is used as-is.

### Converting to Brightness Percent

If `brightness.curve` is missing or has fewer than two points, linear mapping is used:

```text
requestedBrightness =
    minPercent + effectiveValue * (maxPercent - minPercent)
```

The result is then rounded to a whole percent.

If `brightness.curve` is present:

1. `effectiveValue` is converted to `lightPercent`:

```text
lightPercent = clamp(effectiveValue * 100.0, 0.0, 100.0)
```

2. two neighboring curve points are found:

```text
(left.lightPercent, left.brightnessPercent)
(right.lightPercent, right.brightnessPercent)
```

3. linear interpolation is performed between them:

```text
ratio = (lightPercent - left.lightPercent) / (right.lightPercent - left.lightPercent)
requestedBrightness =
    left.brightnessPercent +
    ratio * (right.brightnessPercent - left.brightnessPercent)
```

If `lightPercent` is left of the first point, the first point brightness is used.
If it is right of the last point, the last point brightness is used.

After that, the result is still clamped:

```text
requestedBrightness = clamp(requestedBrightness, minPercent, maxPercent)
```

### Hysteresis Formula

If there is already a previously applied brightness `lastAppliedBrightness`, the application compares it with the new request:

```text
abs(requestedBrightness - lastAppliedBrightness) < hysteresisPercent
```

Important:

- the comparison does not happen immediately after reading `raw`;
- it is not comparing against the previous `raw`;
- it is not comparing against the previous `normalized` or previous `EMA`;
- it happens after normalization, smoothing, gamma correction, curve interpolation, and range clamping;
- the actual comparison is between:
  - the new `requestedBrightness` from the current cycle;
  - the previous `lastAppliedBrightness` that was really applied before.

If the condition is true, the new brightness is not sent to the monitor.
This prevents small sensor fluctuations from causing unnecessary changes.

### Maximum Step Limit

If the change should still be applied, the application also limits how fast brightness can move:

```text
delta = requestedBrightness - lastAppliedBrightness
```

If:

```text
abs(delta) > maxBrightnessStepPercent
```

then only one step is taken:

```text
targetBrightness =
    lastAppliedBrightness + sign(delta) * maxBrightnessStepPercent
```

Otherwise:

```text
targetBrightness = requestedBrightness
```

`targetBrightness` is what gets sent further to `WMI` or `DDC/CI`.

### What This Means Practically

- `raw` is not a ready-made brightness percentage;
- even `normalized` is not the final brightness;
- the result depends on smoothing, gamma, the response curve, range limits, hysteresis, and step limiting.

That is why `raw` must not be treated as a direct brightness value. It is only the input to the brightness pipeline.

## 2. Sending Brightness to the Monitor through `DDC/CI`

## 2.1. What `DDC/CI` Means in This Project

For external monitors, the current Windows implementation uses `DDC/CI` through `dxva2.dll`.

At code level this means:

- the app enumerates displays through `EnumDisplayMonitors`;
- it gets physical monitors through `GetPhysicalMonitorsFromHMONITOR`;
- it tries to read brightness through `GetMonitorBrightness`;
- it tries to write brightness through `SetMonitorBrightness`.

So the working model is:

1. the sensor sends `raw`;
2. the application computes target brightness in the `0..100` range;
3. the application sends that target to the Windows monitor API;
4. Windows, the GPU driver, and the monitor stack deliver the command over `DDC/CI`.

## 2.2. What This Means Practically for the Current Project

In the current project, the data path is:

- `ESP32-C6` sends telemetry to `pc-app`;
- `pc-app` computes the target brightness;
- `pc-app` applies monitor brightness through `WMI` or `DDC/CI`.

There is no direct `I2C` bus from `ESP32-C6` to the monitor in the current implementation.

## 2.3. How to Check `DDC/CI` Brightness Support

Before relying on `DDC/CI`, check the monitor in this order:

1. the monitor should be an external display, not a built-in laptop panel;
2. the monitor OSD should ideally have a setting such as `DDC/CI = On`;
3. the monitor should be connected through an interface and cable that actually passes `DDC/CI`;
4. the Windows API should be able to discover the physical monitor;
5. at least one of these calls should succeed:
   - `GetMonitorBrightness(...)`
   - `SetMonitorBrightness(...)`

For this project, the practical support rule is:

- if the monitor appears through `DdcMonitor.Discover()`, that alone is not a full guarantee;
- if `GetMonitorBrightness(...)` or `SetMonitorBrightness(...)` works reliably, the monitor can be treated as usable for `DDC/CI` brightness control;
- if both fail, the monitor should be treated as unsupported for this method.

## 2.4. Current Implementation Behavior

The current implementation already does a reasonable support check:

- it first discovers monitors through `WMI` and `DDC/CI`;
- for `DDC/CI`, it tries to read the brightness range before writing;
- if reading the range fails, it still tries `SetMonitorBrightness(...)` with a `0..100` percentage;
- if writing fails, the monitor is treated as problematic.

This is useful because some monitors do not report brightness properly but still accept writes.

## 2.5. Practical Support States

For this project, a monitor can be treated as:

- `supported`:
  - it was discovered as a physical monitor;
  - at least one test brightness write succeeded.
- `partially_supported`:
  - brightness read does not work;
  - brightness write works.
- `unsupported`:
  - the monitor is not discovered as controllable;
  - or brightness write fails.

## 2.6. Recommended Application Order

The correct order is:

1. discover monitor-control candidates;
2. separate built-in displays (`WMI`) from external ones (`DDC/CI`);
3. verify practical read/write capability for each monitor;
4. only then enable automatic brightness updates;
5. if a specific monitor keeps failing, disable that monitor instead of breaking the whole sensor pipeline.

This is the most robust approach when some monitors are only partially compatible.

## Related Documents

- [protocol.md](./protocol.md) - current wire contract
- [firmware.md](./firmware.md) - current `ESP32-C6` firmware
- [build.md](./build.md) - running the Windows application
