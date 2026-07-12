# Acceptance Criteria: ESP32-C6 Animated Screen Modernization

## Documentation Deliverable

1. The feature directory contains `FEATURE_SPEC.md`, `IMPLEMENTATION_PLAN.md`, and `ACCEPTANCE_CRITERIA.md`.
2. All three documents are written in English.
3. The implementation remains confined to ESP32-C6 firmware, its asset tooling, and aligned documentation; the Windows application is unchanged.

## Asset And Rendering

1. The approved master asset is a lossless, opaque `160x774` PNG.
2. It contains exactly nine vertical `160x86` frames with no padding or separator rows.
3. Frames are ordered from darkest/closed at index 0 to brightest/open at index 8.
4. Every source pixel becomes an exact `2x2` output block.
5. Each rendered frame fills the `320x172` LCD without cropping, margins, interpolation, or antialiasing.
6. The embedded progress bar is rendered as part of each frame.
7. No separate continuous progress bar is drawn.
8. Frame data is stored at source resolution as 8-bit shared-palette indexes rather than nine full-screen RGB565 images.
9. Asset data remains read-only in flash and does not require a second full-screen framebuffer.

## Frame Selection

1. Ambient light is clamped to `0..100` before frame selection.
2. `0%` targets frame 0.
3. `100%` targets frame 8.
4. Intermediate percentages are divided monotonically and evenly across all nine frames.
5. Rising percentages cannot select a lower frame.
6. Falling percentages cannot select a higher frame.
7. Frame boundaries use a 2 percentage-point hysteresis.
8. Repeated readings within the hysteresis margin do not alternate the accepted target frame.
9. Endpoint clamping does not prevent frame 0 at 0% or frame 8 at 100%.

## Animation

1. The displayed frame moves toward the target by one adjacent frame every `150 ms`.
2. Opening transitions use ascending frame order.
3. Closing transitions use descending frame order.
4. Normal transitions do not skip intermediate frames.
5. A new target during a transition takes effect immediately.
6. Retargeting continues from the currently displayed frame without jumping or restarting.
7. A delayed render does not cause multiple frames to be skipped in one update.
8. Animation scheduling does not change sensor sampling or USB telemetry cadence.

## Text Overlay

1. The current normalized ambient-light percentage remains visible.
2. The raw reading remains visible in `ADC ####` form.
3. Both values are placed in the upper-left safe area.
4. The percentage uses a medium pixel-font size and the ADC line uses a smaller size.
5. Every supported value through `100%` and the expected ADC range fits the reserved area.
6. Text does not cover the flower or the embedded progress bar.
7. Text uses a bright foreground with a one-pixel dark pixel outline.
8. Text has no rectangular background panel or antialiasing.

## Runtime States

1. Before the first valid reading, the screen shows frame 0, `--%`, and `ADC ----`.
2. The flower does not begin opening before a valid reading is available.
3. A valid reading restores numeric percentage and ADC text.
4. A sensor read error preserves the currently displayed frame.
5. During an error, the percentage position shows `ERR` and the ADC line shows an explicit error indication.
6. The firmware logs the underlying sensor or rendering failure through its existing logging path.
7. After sensor recovery, numeric text returns and animation retargets from the preserved frame.
8. Framebuffer allocation and LCD flush failures remain safely reported.

## Compatibility

1. Telemetry remains newline-delimited JSON with `id="lumabloom"`, `ts`, and `raw` fields.
2. The telemetry send interval remains unchanged.
3. KY-018 reading, normalization, inversion, gamma, and raw-range behavior remain unchanged.
4. Calibration command compatibility remains unchanged.
5. The Windows `pc-app` requires no change for this feature.
6. LCD dimensions, orientation, offsets, and ST7789 initialization remain unchanged.
7. LCD backlight brightness remains controlled by the existing fixed configuration.

## Build And Physical Validation

1. `idf.py build` succeeds for target `esp32c6` without new undocumented warnings.
2. The firmware starts on the Waveshare `ESP32-C6-LCD-1.47` without framebuffer allocation failure.
3. All nine frames are visually inspected on the physical LCD for correct color, orientation, scale, and order.
4. Boundary values are tested in both rising and falling directions.
5. Noise within +/-2 percentage points of a boundary does not visibly flicker between frames.
6. A complete 0-to-100 and 100-to-0 transition displays every frame in order.
7. Mid-transition target changes reverse or redirect smoothly from the visible frame.
8. Startup, sensor failure, and recovery states are visually verified.
9. Serial telemetry is captured during animation and remains compatible with `pc-app`.

## Manual Test Plan

1. Boot with no usable sensor reading and verify frame 0 with startup placeholders.
2. Provide a stable 0% reading and verify frame 0.
3. Increase through every frame boundary and verify hysteresis and ordered animation.
4. Hold readings around a boundary within the hysteresis margin and verify stability.
5. Jump from a low reading to 100% and verify every intermediate opening frame.
6. Jump from a high reading to 0% and verify every intermediate closing frame.
7. Reverse the light change during an active transition and verify immediate retargeting.
8. Force a KY-018 read failure and verify the preserved frame plus error text.
9. Restore the sensor and verify numeric text and animation recovery.
10. Confirm percentage and ADC readability at the darkest and brightest frames.
11. Monitor USB JSONL output throughout the test and compare it with the existing protocol contract.
