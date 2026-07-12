# Feature Spec: ESP32-C6 Animated Screen Modernization

## Summary

Modernize the LumaBloom ESP32-C6 LCD with a full-screen pixel-art flower animation while preserving the current ambient-light percentage and raw ADC diagnostics.

The animation visualizes the locally normalized ambient-light level. It is limited to the firmware display path and must not change sensor processing, serial telemetry, calibration compatibility, LCD backlight behavior, or the Windows application.

The approved sprite sheet is stored with the firmware assets and converted into a deterministic palette-indexed C module for the device build.

## Target

- Board: Waveshare `ESP32-C6-LCD-1.47`.
- Display: horizontal `320x172` ST7789 LCD.
- Firmware: `firmware/firmware_esp32c6/`.
- Source sprite sheet: opaque PNG, `160x774` pixels.
- Frame layout: nine `160x86` frames arranged vertically.
- Frame order: frame 0 is the darkest/closed state; frame 8 is the brightest/fully open state.

## Full-Screen Artwork

Each source frame is scaled exactly 2x to fill the `320x172` display.

Rendering requirements:

1. Use nearest-neighbor scaling only.
2. Expand every source pixel into one `2x2` block.
3. Do not use filtering, interpolation, antialiasing, cropping, or display margins.
4. Preserve the progress bar drawn into each frame.
5. Do not draw a separate continuous progress bar over the artwork.

## Ambient-Light Mapping

The locally normalized ambient-light percentage remains clamped to `0..100` and determines a target frame in `0..8`.

The range is divided evenly across all nine frames. The implementation must use one documented integer mapping consistently so that:

- `0%` selects frame 0;
- `100%` selects frame 8;
- increasing light never selects a lower target frame;
- decreasing light never selects a higher target frame.

Frame selection uses a 2 percentage-point hysteresis around each boundary. A reading must cross the active boundary by the hysteresis margin before the target moves into an adjacent range. This prevents repeated opening and closing when readings fluctuate near a boundary.

## Animation

The current displayed frame moves toward the target frame one step at a time.

- Interval between adjacent frames: `150 ms`.
- Rising light advances the animation in ascending frame order.
- Falling light reverses it in descending frame order.
- Intermediate frames must never be skipped during a normal transition.
- The first usable reading may establish the target, but the displayed flower still starts at frame 0 and animates toward it.

If the target changes while a transition is running, the animation immediately retargets from the frame currently visible. It does not finish the obsolete transition, restart at an endpoint, or jump directly to the new target.

Animation timing must be independent from sensor sampling and USB telemetry timing. Display animation must not delay sensor reads or change telemetry cadence.

## Text Overlay

The display continues to show:

- the locally normalized ambient-light percentage;
- the raw sensor value as `ADC ####`.

Both values appear in the upper-left safe area:

- the percentage uses a medium pixel-font size suitable for values through `100%`;
- the ADC line uses a smaller pixel-font size below the percentage;
- text must not cover the flower or the embedded progress bar.

Text uses a bright foreground color from the artwork palette with a one-pixel dark pixel outline. It has no rectangular background panel. The outline must remain crisp and must not introduce antialiasing.

## Runtime States

### Waiting For First Reading

Before the first valid sensor reading:

- show frame 0;
- show `--%` in the percentage position;
- show `ADC ----` in the ADC position;
- do not begin opening the flower.

### Valid Reading

After a valid reading:

- show the current normalized percentage and raw ADC value;
- update the target frame through the mapping and hysteresis rules;
- animate from the current frame toward the accepted target.

### Sensor Read Error

If a KY-018 read fails after the UI has valid data:

- preserve the last displayed flower frame;
- replace the percentage with `ERR`;
- replace the ADC value with an explicit ADC error indication that fits the overlay region;
- report the underlying error through existing firmware logging;
- do not fabricate a percentage or force the flower closed.

When valid readings resume, restore numeric text and retarget the animation from the preserved frame.

## Compatibility And Non-Goals

This feature must not change:

- `APP_PROTOCOL_ID` or the required telemetry `id="lumabloom"`;
- the raw JSONL telemetry shape or send interval;
- KY-018 sampling, normalization, inversion, gamma, or configured raw range;
- calibration command compatibility;
- the Windows `pc-app`;
- the fixed LCD backlight percentage;
- display orientation, dimensions, or panel initialization.

The feature does not add interactive controls, new serial commands, runtime themes, sprite selection, smooth sub-frame morphing, or a separately rendered progress bar.

## Asset Contract

The firmware keeps the master sprite sheet under its asset directory and generates a firmware-friendly representation from it.

The accepted master asset contract is:

- lossless PNG;
- exactly `160x774`;
- exactly nine vertical `160x86` frames;
- fully opaque pixels;
- consistent shared limited palette;
- no gaps, padding, or separator rows between frames;
- darkest/closed to brightest/open ordering from top to bottom.

Future sprite revisions must preserve this contract unless the feature documentation and conversion tooling are updated together.
