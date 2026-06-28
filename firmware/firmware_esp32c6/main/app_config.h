#pragma once

#include "driver/gpio.h"
#include "driver/spi_common.h"
#include "esp_adc/adc_oneshot.h"

#define APP_DEVICE_ID "esp32c6-01"
#define APP_SENSOR_ID "light0"

#define APP_READ_INTERVAL_MS 200
#define APP_DISPLAY_INTERVAL_MS 200

#define APP_KY018_ADC_UNIT ADC_UNIT_1
#define APP_KY018_ADC_CHANNEL ADC_CHANNEL_4
#define APP_KY018_ADC_GPIO GPIO_NUM_4
#define APP_KY018_ADC_MIN 0
#define APP_KY018_ADC_MAX 4095
#define APP_KY018_INVERT 1
#define APP_KY018_GAMMA 1.0f

#define APP_LCD_HOST SPI2_HOST
#define APP_LCD_PIXEL_CLOCK_HZ (40 * 1000 * 1000)
#define APP_LCD_WIDTH 320
#define APP_LCD_HEIGHT 172
#define APP_LCD_X_OFFSET 0
#define APP_LCD_Y_OFFSET 34
#define APP_LCD_SPI_MOSI GPIO_NUM_6
#define APP_LCD_SPI_CLK GPIO_NUM_7
#define APP_LCD_CS GPIO_NUM_14
#define APP_LCD_DC GPIO_NUM_15
#define APP_LCD_RST GPIO_NUM_21
#define APP_LCD_BL GPIO_NUM_22
