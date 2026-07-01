# Enclosure 3D Printing

This directory contains the printable enclosure and decorative body for the LumaBloom ESP32-C6 sensor build.

## Layout

| Path | Purpose |
| --- | --- |
| `enclosure/` | Slicer-ready `.3mf` plates grouped by filament color |
| `images/` | Preview and per-color reference images |
| `source/` | STEP source models and selected exported STL parts |
| `LICENSE.md` | License notes for physical-design assets |

## Print Files

The slicer-ready files are grouped by color:

| File | Suggested material / color | Reference image |
| --- | --- | --- |
| `enclosure/White parts.3mf` | White / petal parts | `images/White parts.png` |
| `enclosure/Green parts.3mf` | Green / stem and leaf parts | `images/Green parts.png` |
| `enclosure/Light brown parts.3mf` | Light brown / pot body parts | `images/Light brown parts.png` |
| `enclosure/Brown parts.3mf` | Brown / soil or accent parts | `images/Brown parts.png` |

Open each `.3mf` in your slicer, review bed placement and support settings, then slice with settings appropriate for your printer and filament.

## Source Models

The `source/` directory contains the full assembly and individual part sources:

| File | Purpose |
| --- | --- |
| `BR-000-AS - Fully assembly.step` | Full mechanical assembly |
| `BR-001-3D - Ball joint.step` | Ball joint |
| `BR-002-3D - Ball joint Mid.step` | Middle ball-joint segment |
| `BR-003-3D - Bottom case.step` | Bottom case |
| `BR-004-3D Wase.step` | Vase / pot body |
| `BR-005-3D - Display Frame.step` | Display frame |
| `BR-006-3D - Wase rim.step` | Vase / pot rim |
| `BR-007-3D - Ground.step` | Ground / soil insert |
| `BR-008-3D - Base Joint.step` | Base joint |
| `BR-009-3D - Bud.step` | Flower center / bud |
| `BR-010-3D - Flower disk.step` | Flower disk, original version |
| `BR-010-3D - Flower disk V2.step` | Flower disk, V2 |
| `BR-011-3D - Shaft.step` | Stem shaft |
| `BR-012-3D - Petal.step` | Petal source model |
| `BR-012-3D Petal V2.stl` | Petal V2 printable/exported mesh |
| `BR-013-3D - Leaf.step` | Leaf source model |
| `BR-013-3D Leaf V2.stl` | Leaf V2 printable/exported mesh |
| `BR-014-3D - Sensor lid.step` | Sensor lid |

## Assembly Notes

- The current enclosure assembly is for the Waveshare `ESP32-C6-LCD-1.47` build.
- Print the color-grouped `.3mf` files before final wiring so the display, sensor, and cable routing can be checked against the case.
- Keep the KY-018 light path unobstructed after installing `BR-014-3D`.
- Route USB and sensor wiring so the ball joints can move without pulling on solder joints or Dupont connectors.
- Follow the full assembly order in `../ASSEMBLY.md`.
- The `.3mf` plates are the preferred print entry point. Use the STEP and STL files only when modifying or exporting replacement parts.

## Images

- `images/Assembled.jpg` - assembled physical device photo.
- `images/main.png` - complete product preview.
- `images/White parts.png` - white print plate reference.
- `images/Green parts.png` - green print plate reference.
- `images/Light brown parts.png` - light-brown print plate reference.
- `images/Brown parts.png` - brown print plate reference.
