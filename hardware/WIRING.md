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
