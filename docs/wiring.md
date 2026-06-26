# Wiring KY-018 -> ESP32-C3

## Pins

- `KY-018 VCC` -> `ESP32-C3 3V3`
- `KY-018 GND` -> `ESP32-C3 GND`
- `KY-018 AO` -> `ESP32-C3 GPIO0` (ADC)

## Notes

- Only the analog output (`AO`) is used.
- For stable readings, keep wires short and use a common GND.
- Some KY-018 modules are labeled `S`, `+`, `-`, where `S` is the signal pin (`AO`).

# Wiring KY-018 -> Waveshare ESP32-C6-LCD-1.47

## Onboard LCD

For the onboard LCD, the project uses these Waveshare pins:

- `MOSI` -> `GPIO6`
- `SCLK` -> `GPIO7`
- `LCD_CS` -> `GPIO14`
- `LCD_DC` -> `GPIO15`
- `LCD_RST` -> `GPIO21`
- `LCD_BL` -> `GPIO22`

Display controller: `ST7789`.

## Default KY-018 Wiring

- `KY-018 VCC` -> `3V3`
- `KY-018 GND` -> `GND`
- `KY-018 AO` -> `GPIO4` (ADC)

## Notes

- Only the analog output (`AO`) is used.
- If your KY-018 is connected to a different ADC pin, update `APP_KY018_ADC_CHANNEL` and the related `APP_KY018_ADC_GPIO` in `firmware/firmware_esp32c6/main/app_config.h`.
- For stable measurements, use a common `GND` and short wires.
- For `ESP32-C6`, using `GPIO0` for `KY-018` is not recommended because it may interfere with normal board startup.
