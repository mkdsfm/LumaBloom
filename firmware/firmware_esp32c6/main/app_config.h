#pragma once

#include "driver/gpio.h"
#include "driver/i2c.h"
#include "driver/spi_common.h"

#define APP_DEVICE_ID "esp32c6-01"
#define APP_SENSOR_ID "light0"

#define APP_READ_INTERVAL_MS 500
#define APP_DISPLAY_INTERVAL_MS 500

#define APP_BH1750_I2C_PORT I2C_NUM_0
#define APP_BH1750_I2C_SDA GPIO_NUM_20
#define APP_BH1750_I2C_SCL GPIO_NUM_23
#define APP_BH1750_I2C_FREQ_HZ 100000
#define APP_BH1750_ADDR 0x23

#define APP_LCD_HOST SPI2_HOST
#define APP_LCD_PIXEL_CLOCK_HZ (40 * 1000 * 1000)
#define APP_LCD_WIDTH 172
#define APP_LCD_HEIGHT 320
#define APP_LCD_X_OFFSET 34
#define APP_LCD_Y_OFFSET 0
#define APP_LCD_SPI_MOSI GPIO_NUM_6
#define APP_LCD_SPI_CLK GPIO_NUM_7
#define APP_LCD_CS GPIO_NUM_14
#define APP_LCD_DC GPIO_NUM_15
#define APP_LCD_RST GPIO_NUM_21
#define APP_LCD_BL GPIO_NUM_22
