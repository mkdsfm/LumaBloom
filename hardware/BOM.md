# Bill Of Materials

## ESP32-C6 Build

| Qty | Part                             | Notes                                                |
|-----|----------------------------------|------------------------------------------------------|
| 1   | Waveshare ESP32-C6-LCD-1.47      | Target board for the ESP-IDF firmware                |
| 1   | KY-018 light sensor module       | Analog output is used                                |
| 3+  | Dupont jumper wires, 20 cm       | `VCC`, `GND`, and `AO`                               |
| 1   | Breadboard or safe mounting base | Optional, but useful for prototyping                 |
| 1   | USB cable                        | Data-capable cable for flashing and serial telemetry |

## ESP32-C3 Super Mini Build

| Qty | Part                             | Notes                                                |
|-----|----------------------------------|------------------------------------------------------|
| 1   | ESP32-C3 Super Mini              | USB must expose built-in USB Serial/JTAG             |
| 1   | KY-018 light sensor module       | Analog output connects to GPIO4                      |
| 3+  | Dupont jumper wires, 20 cm       | `VCC`, `GND`, and `AO`                               |
| 1   | Breadboard or safe mounting base | Optional, but useful for prototyping                 |
| 1   | USB data cable                   | Used for flashing, power, and serial telemetry       |

The ESP32-C3 build uses the same flower, KY-018 holder, stem, joints, petals, leaves, wiring, and fasteners as the ESP32-C6 build. Its board-specific lower pot assembly has not been modeled or published yet.

## Printable Flower Enclosure

| Qty       | Part                      | Notes                                                 |
|-----------|---------------------------|-------------------------------------------------------|
| 1 set     | White printed parts       | Print from `3d-print/enclosure/White parts.3mf`       |
| 1 set     | Green printed parts       | Print from `3d-print/enclosure/Green parts.3mf`       |
| 1 set     | Light-brown printed parts | Print from `3d-print/enclosure/Light brown parts.3mf` |
| 1 set     | Brown printed parts       | Print from `3d-print/enclosure/Brown parts.3mf`       |
| As needed | Heat-set threaded inserts | Installed into `BR-003-3D` and `BR-006-3D`            |
| 2         | `M3x6` screws             | Fasten `BR-003-3D` to `BR-006-3D`                     |
| 2         | `M2x5` screws             | Fasten the `BR-014-3D` sensor lid to `BR-009-3D`      |
| 2         | Board mounting screws     | Mount the ESP32-C6 board to `BR-003-3D` diagonally    |
| As needed | Printed `BR-011-3D` pins  | Used in the vase rim and bud / stem assembly          |

The current color-grouped `.3mf` plates include the ESP32-C6 lower pot parts. The shared flower parts can also be reused for the ESP32-C3 build, but a complete C3 print set will require the future ESP32-C3 pot and board mount. The STEP sources and selected STL exports live in `3d-print/source/`.
