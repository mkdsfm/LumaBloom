# Hardware Revisions

## Current Prototype

### ESP32-C6 + KY-018 + LCD

- Board: Waveshare `ESP32-C6-LCD-1.47`.
- Sensor: KY-018 analog light sensor.
- Default signal pin: `GPIO4`.
- Firmware track: ESP-IDF.
- Telemetry semantics: calibrated normalized `0..1000` value plus optional raw ADC diagnostics.
- Important constraint: avoid `GPIO0` for the sensor signal because it can affect normal startup.

### LumaBloom ESP32-C6 Enclosure

- Current enclosure target: Waveshare `ESP32-C6-LCD-1.47` only.
- Printable plates: `White parts.3mf`, `Green parts.3mf`, `Light brown parts.3mf`, and `Brown parts.3mf`.
- Required assembly hardware includes heat-set threaded inserts, two `M3x6` screws, two `M2x5` screws, ESP32-C6 board mounting screws, and 20 cm Dupont jumper wires.
- Assembly uses `BR-003-3D` as the bottom case, `BR-004-3D` as the vase body, `BR-005-3D` as the display frame, `BR-006-3D` as the vase rim, `BR-009-3D` as the sensor bud, and `BR-014-3D` as the sensor lid.

## Change Log

| Revision | Date | Notes |
| --- | --- | --- |
| Prototype | 2026-07-01 | Initial hardware documentation split into `hardware/` with BOM, wiring, assembly, and a 3D-print asset structure. |
| LumaBloom enclosure assets | 2026-07-01 | Added color-grouped `.3mf` print plates, preview images, and STEP/STL source files for the flower enclosure. |
| C6 enclosure assembly | 2026-07-01 | Scoped enclosure assembly to the ESP32-C6 build, documented heat-set inserts, `M3x6` / `M2x5` screws, KY-018 installation, cable routing, and `BR-005-3D` display-frame insertion. |
| BOM cable length | 2026-07-01 | Set Dupont jumper wire length to 20 cm for the documented hardware builds. |
