# Implementation Plan: ESP32-C6 Animated Screen Modernization

## Scope

Target: `firmware/firmware_esp32c6/`.

Implementation status: implemented. The master PNG, deterministic converter, generated palette asset, LCD primitives, UI state, and build integration are present in the ESP32-C6 firmware track.

## Phase 1: Asset Source And Conversion

1. Add the approved master PNG under a firmware asset/source directory.
2. Validate the asset contract before conversion:
   - dimensions are `160x774`;
   - there are nine vertical `160x86` frames;
   - all pixels are opaque;
   - frames are ordered closed-to-open;
   - the palette contains no more than 256 colors.
3. Convert the shared source palette to BGR565 values matching the configured LCD color order.
4. Convert every source pixel to an 8-bit palette index.
5. Generate a firmware asset module containing:
   - frame count;
   - source width and height;
   - the shared RGB565 palette;
   - nine fixed-size indexed frame arrays.
6. Keep the generated representation deterministic so regenerating from an unchanged PNG produces no diff.

The inspected reference PNG contains six opaque colors. An 8-bit index per source pixel therefore stores all nine low-resolution frames in about 124 KB before compiler/linker overhead, instead of storing approximately 991 KB of full-screen RGB565 frames.

## Phase 2: LCD Drawing Primitives

1. Add a primitive that draws one `160x86` indexed frame into the existing `320x172` RGB565 framebuffer.
2. Resolve each palette index to RGB565 and write it as a `2x2` pixel block.
3. Reject or safely ignore invalid frame indexes without writing outside the framebuffer.
4. Add outlined pixel-text rendering:
   - draw the same glyph at the eight neighboring one-pixel offsets in the outline color;
   - draw the foreground glyph last at its requested position;
   - preserve existing plain-text drawing functions for other callers.
5. Continue using the existing full-frame `display_lcd_flush()` path.

No additional full-screen framebuffer is required. Asset arrays must remain read-only in flash; only the existing RGB565 framebuffer is mutable at runtime.

## Phase 3: UI State And Frame Mapping

Extend the UI state to track:

- whether any valid reading has been received;
- whether the latest sensor operation failed;
- the most recent valid normalized percentage and raw ADC value;
- current displayed frame;
- current target frame;
- last percentage accepted for a frame-range transition;
- timestamp of the last animation step.

Implement one integer percentage-to-frame mapping shared by production code and tests. It must divide `0..100` monotonically across frames `0..8`, with exact endpoint guarantees.

Apply 2 percentage-point hysteresis relative to the boundary adjacent to the currently accepted range:

- an upward move is accepted only after the percentage reaches the next boundary plus the margin;
- a downward move is accepted only after the percentage reaches the previous boundary minus the margin;
- clamp endpoint behavior so frame 0 remains reachable at 0% and frame 8 remains reachable at 100%;
- allow a large reading change to select its final target range in one update while animation still traverses intermediate frames.

## Phase 4: Animation Timing

1. Keep sensor sampling and telemetry scheduling unchanged.
2. Evaluate animation progress from elapsed time rather than assuming sensor callbacks occur every `150 ms`.
3. When at least `150 ms` has elapsed and current frame differs from target:
   - increment by one if target is higher;
   - decrement by one if target is lower;
   - record the animation-step timestamp.
4. When a new target arrives mid-transition, replace the target immediately and continue from the visible frame.
5. Never skip an intermediate frame to catch up after a delayed render; resume one step per subsequent animation interval.
6. Render when text state changes or an animation step occurs, without increasing sensor or telemetry frequency.

## Phase 5: Screen Composition And States

Render in this order:

1. Decode the current flower frame into the framebuffer.
2. Draw the outlined percentage in the upper-left safe region at a medium font scale.
3. Draw the smaller outlined ADC line below it.
4. Flush the completed framebuffer.

Choose fixed overlay coordinates and font scales against the approved master frame, then record them as named UI layout constants. All possible strings (`100%`, `ADC 4095`, `--%`, `ADC ----`, `ERR`, and the ADC error label) must fit without touching the flower or progress bar.

State behavior:

- startup: frame 0, `--%`, `ADC ----`;
- valid reading: numeric percentage and ADC text, with normal target/animation updates;
- read failure: preserve the current frame, show `ERR` and the ADC error label;
- recovery: restore numeric text and retarget from the preserved frame.

Keep framebuffer allocation and panel flush error handling logged through the existing ESP-IDF logging path.

## Phase 6: Integration

1. Add the asset module and any conversion output to the firmware component source list.
2. Connect sensor read success and failure paths to explicit UI state updates.
3. Preserve the public telemetry and calibration paths unchanged.
4. Preserve `APP_LCD_BACKLIGHT_PERCENT` and the existing LEDC backlight configuration.
5. Update firmware-facing documentation after implementation to describe the animated screen and asset regeneration workflow.

## Validation

Host-test pure mapping, hysteresis, and animation-state logic where practical. Build and visually validate on the physical Waveshare board.

From an ESP-IDF shell:

```powershell
cd firmware\firmware_esp32c6
idf.py set-target esp32c6
idf.py build
idf.py -p COMx flash monitor
```

During physical validation, exercise startup, stable boundary readings, rapid large changes in both directions, mid-transition retargeting, sensor failure/recovery, and USB telemetry alongside the animation.
