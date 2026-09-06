# Wiring

## KY-018 -> Waveshare ESP32-C6-LCD-1.47

### Onboard LCD

The onboard LCD is already wired on the Waveshare board. The firmware uses these pins:

| LCD signal | ESP32-C6 GPIO |
| --- | --- |
| `MOSI` | `GPIO6` |
| `SCLK` | `GPIO7` |
| `LCD_CS` | `GPIO14` |
| `LCD_DC` | `GPIO15` |
| `LCD_RST` | `GPIO21` |
| `LCD_BL` | `GPIO22` |

Display controller: `ST7789`.

### Default KY-018 Pins

| KY-018 | Waveshare ESP32-C6-LCD-1.47 |
| --- | --- |
| `VCC` | `3V3` |
| `GND` | `GND` |
| `AO` / `S` | `GPIO4` (ADC) |

### Notes

- Only the analog output (`AO`) is used.
- If your KY-018 is connected to a different ADC pin, update `APP_KY018_ADC_CHANNEL` and the related `APP_KY018_ADC_GPIO` in `firmware/firmware_esp32c6/main/app_config.h`.
- For stable measurements, use a common `GND` and short wires.
- Using `GPIO0` for `KY-018` on ESP32-C6 is not recommended because it may interfere with normal board startup.

## KY-018 -> ESP32-C3 Super Mini

| KY-018 | ESP32-C3 Super Mini |
| --- | --- |
| `VCC` / `+` | `3V3` |
| `GND` / `-` | `GND` |
| `AO` / `S` | `GPIO4` (`ADC1_CH4`) |

### Notes

- This track targets Super Mini boards whose USB connector is wired to the ESP32-C3 built-in USB Serial/JTAG controller.
- Keep GPIO18 and GPIO19 free because they carry USB D- and D+.
- Power the KY-018 from `3V3`, not `5V`.
- Only the analog output is used; the firmware sends its native 12-bit ADC result.
- The upper flower and sensor-holder parts are shared with the ESP32-C6 enclosure. The ESP32-C3 Super Mini requires a different lower pot and board mount, which have not been modeled or published yet.
