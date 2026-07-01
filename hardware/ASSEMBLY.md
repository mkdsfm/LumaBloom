# Assembly

Current enclosure assembly is documented for the Waveshare `ESP32-C6-LCD-1.47` build only.

## Before You Start

- Use the ESP32-C6 firmware track in `firmware/firmware_esp32c6/`.
- Keep the USB cable disconnected while changing wiring or installing the board.
- Use `3V3` for the KY-018 power pin.
- Do not use `GPIO0` for the KY-018 signal on ESP32-C6; it can interfere with normal startup.
- Print the enclosure plates from `3d-print/enclosure/` before final assembly.

## Required Printed Parts

| Part | Source file |
| --- | --- |
| `BR-001-3D` | `3d-print/source/BR-001-3D - Ball joint.step` |
| `BR-002-3D` | `3d-print/source/BR-002-3D - Ball joint Mid.step` |
| `BR-003-3D` | `3d-print/source/BR-003-3D - Bottom case.step` |
| `BR-004-3D` | `3d-print/source/BR-004-3D Wase.step` |
| `BR-005-3D` | `3d-print/source/BR-005-3D - Display Frame.step` |
| `BR-006-3D` | `3d-print/source/BR-006-3D - Wase rim.step` |
| `BR-007-3D` | `3d-print/source/BR-007-3D - Ground.step` |
| `BR-008-3D` | `3d-print/source/BR-008-3D - Base Joint.step` |
| `BR-009-3D` | `3d-print/source/BR-009-3D - Bud.step` |
| `BR-011-3D` | `3d-print/source/BR-011-3D - Shaft.step` |
| `BR-014-3D` | `3d-print/source/BR-014-3D - Sensor lid.step` |

## Assembly Steps

1. Flash the ESP32-C6 board with the firmware from `firmware/firmware_esp32c6/`.
2. Print the enclosure details from `3d-print/enclosure/`.
3. Heat-set the threaded inserts into `BR-003-3D`.
4. Heat-set the threaded inserts into `BR-006-3D`.
5. Insert the `BR-011-3D` pins into `BR-006-3D`, then insert the ground / soil part `BR-007-3D`.
6. Fasten `BR-003-3D` to `BR-006-3D` with two `M3x6` screws.
7. Take `BR-009-3D` and attach the second-version `BR-011-3D` shaft using two `BR-011-3D` pins.
8. Attach the ESP32-C6 board to `BR-003-3D` with two diagonal screws.
9. Connect the cable to the KY-018, insert the sensor into `BR-009-3D`, close it with the `BR-014-3D` sensor lid, and fasten the lid with two `M2x5` screws.
10. Pass the cable through the slotted hole in `BR-002-3D`, then connect `BR-002-3D` to `BR-009-3D` with the ball joint.
11. Pass the cable through the slotted hole in `BR-001-3D`, then connect it with ball joints to `BR-002-3D` and `BR-008-3D`.
12. Insert the assembled device stem into the top of the vase at `BR-006-3D`.
13. Connect the cables to the ESP32-C6 board.
14. Insert the assembled `BR-003-3D` part into `BR-004-3D`.
15. Insert the display frame `BR-005-3D` into `BR-004-3D`.
16. Done.

## Wiring Check

Default KY-018 wiring for the ESP32-C6 build:

| KY-018 | Waveshare ESP32-C6-LCD-1.47 |
| --- | --- |
| `VCC` | `3V3` |
| `GND` | `GND` |
| `AO` / `S` | `GPIO4` (ADC) |

## Smoke Check

After enclosure assembly:

1. Connect the device over USB.
2. Start the Windows app from `pc-app/` so it can send the startup calibration command.
3. Confirm that before calibration the LCD shows `--%`.
4. Confirm that after calibration the LCD shows a percentage and telemetry includes `value`, `raw`.

## Signal Quality

- Keep the analog signal wire short.
- Use a shared `GND` between the board and sensor.
- Avoid routing the signal wire near noisy USB hubs, power supplies, or display cables.
- Move the ball-joint / stem assembly through its expected range and confirm no wire is pulled tight.
