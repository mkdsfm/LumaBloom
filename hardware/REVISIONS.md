# Hardware Revisions

## Current Prototype

### ESP32-C6 + KY-018 + LCD

- Board: Waveshare `ESP32-C6-LCD-1.47`.
- Sensor: KY-018 analog light sensor.
- Default signal pin: `GPIO4`.
- Firmware track: ESP-IDF.
- Telemetry semantics: raw ADC value on the USB contract, with normalized percent kept local to the LCD/UI.
- Important constraint: avoid `GPIO0` for the sensor signal because it can affect normal startup.

### ESP32-C3 Super Mini + KY-018

- Board: ESP32-C3 Super Mini with built-in USB Serial/JTAG exposed on USB.
- Sensor: the same KY-018 analog light sensor used by the ESP32-C6 build.
- Default signal pin: `GPIO4` (`ADC1_CH4`).
- Firmware track: ESP-IDF, without a display task.
- Telemetry semantics: the same raw ADC value on the USB contract.
- Mechanical status: the flower above the pot is shared; the different ESP32-C3 lower pot and board mount are not yet available.

### LumaBloom Enclosure

- The upper flower assembly is shared between the ESP32-C6 and ESP32-C3 hardware tracks.
- The current lower pot and board mount target the Waveshare `ESP32-C6-LCD-1.47`.
- A different lower pot and board mount are planned for ESP32-C3 Super Mini but are not yet available.
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
| C3 enclosure scope | 2026-09-07 | Documented the shared flower assembly and the missing board-specific ESP32-C3 lower pot and mount. |
