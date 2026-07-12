#pragma once

#include "driver/gpio.h"
#include "driver/spi_common.h"
#include "esp_adc/adc_oneshot.h"

#define APP_PROTOCOL_ID "lumabloom"

// Sensor reads stay at 200 ms. The display task ticks more often so it can
// advance the animation independently between sensor readings.
#define APP_READ_INTERVAL_MS 200
#define APP_DISPLAY_INTERVAL_MS 50

// User-facing animation tuning:
// - lower FRAME_INTERVAL for faster flower transitions;
// - increase HYSTERESIS if the flower flickers near a frame boundary.
#define APP_ANIMATION_FRAME_INTERVAL_MS 150
#define APP_ANIMATION_HYSTERESIS_PERCENT 2

#define APP_KY018_ADC_UNIT ADC_UNIT_1
#define APP_KY018_ADC_CHANNEL ADC_CHANNEL_4
#define APP_KY018_ADC_GPIO GPIO_NUM_4
#define APP_KY018_ADC_MIN 200
#define APP_KY018_ADC_MAX 3200
#define APP_KY018_INVERT 1
#define APP_KY018_GAMMA 2.0f

#define APP_LCD_HOST SPI2_HOST
#define APP_LCD_PIXEL_CLOCK_HZ (40 * 1000 * 1000)
#define APP_LCD_WIDTH 320
#define APP_LCD_HEIGHT 172
// These offsets select the visible 320x172 area inside the ST7789 memory.
// They are panel geometry settings, not UI element coordinates.
#define APP_LCD_X_OFFSET 0
#define APP_LCD_Y_OFFSET 34
#define APP_LCD_SPI_MOSI GPIO_NUM_6
#define APP_LCD_SPI_CLK GPIO_NUM_7
#define APP_LCD_CS GPIO_NUM_14
#define APP_LCD_DC GPIO_NUM_15
#define APP_LCD_RST GPIO_NUM_21
#define APP_LCD_BL GPIO_NUM_22
#define APP_LCD_BACKLIGHT_PERCENT 10
