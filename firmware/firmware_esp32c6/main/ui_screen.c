#include "ui_screen.h"

#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#include "display_lcd.h"
#include "esp_log.h"
#include "app_config.h"

static const char *TAG = "ui_screen";

static const uint16_t COLOR_BG = 0x0000;
static const uint16_t COLOR_PRIMARY = 0xFFFF;

static const int MAIN_VALUE_X = 0;
static const int MAIN_VALUE_Y = 46;
static const int MAIN_VALUE_W = 320;
static const int MAIN_VALUE_H = 56;
static const int MAIN_VALUE_SCALE = 8;

static const int ADC_VALUE_X = 0;
static const int ADC_VALUE_Y = 112;
static const int ADC_VALUE_W = 320;
static const int ADC_VALUE_H = 14;
static const int ADC_SCALE = 2;

typedef struct {
    int brightness_percent;
    int adc_raw;
    bool calibrated;
} ui_state_t;

static ui_state_t s_ui_state = {
    .brightness_percent = 0,
    .adc_raw = 0,
    .calibrated = false,
};

static const char *percentage_text(void)
{
    static char buffer[8];

    if (!s_ui_state.calibrated) {
        return "--%";
    }

    snprintf(buffer, sizeof(buffer), "%d%%", s_ui_state.brightness_percent);
    return buffer;
}

static void adc_text(char *buffer, size_t buffer_size)
{
    snprintf(buffer, buffer_size, "ADC %d", s_ui_state.adc_raw);
}

static int clamp_percent(int value)
{
    if (value < 0) {
        return 0;
    }
    if (value > 100) {
        return 100;
    }
    return value;
}

static int ui_y_rect(int y, int h)
{
    return APP_LCD_HEIGHT - y - h;
}

static void ui_draw_text_centered_in_region(int x, int y, int w, int h, const char *text, uint16_t color, int scale)
{
    const int center_x = x + (w / 2);
    display_lcd_draw_text_centered(center_x, ui_y_rect(y, h), text, color, scale);
}

static void draw_main_value(void)
{
    const char *text = percentage_text();
    ui_draw_text_centered_in_region(
        MAIN_VALUE_X,
        MAIN_VALUE_Y,
        MAIN_VALUE_W,
        MAIN_VALUE_H,
        text,
        COLOR_PRIMARY,
        MAIN_VALUE_SCALE);
}

static void draw_adc_line(void)
{
    char buffer[16];
    adc_text(buffer, sizeof(buffer));
    ui_draw_text_centered_in_region(
        ADC_VALUE_X,
        ADC_VALUE_Y,
        ADC_VALUE_W,
        ADC_VALUE_H,
        buffer,
        COLOR_PRIMARY,
        ADC_SCALE);
}

esp_err_t ui_screen_init(void)
{
    return display_lcd_init();
}

void ui_update_reading(int brightness_percent, int adc_raw, bool calibrated)
{
    s_ui_state.brightness_percent = clamp_percent(brightness_percent);
    s_ui_state.adc_raw = adc_raw < 0 ? 0 : adc_raw;
    s_ui_state.calibrated = calibrated;
}

void ui_screen_render(void)
{
    if (!display_lcd_is_ready()) {
        return;
    }

    display_lcd_fill_screen(COLOR_BG);
    draw_main_value();
    draw_adc_line();

    if (display_lcd_flush() != ESP_OK) {
        ESP_LOGE(TAG, "panel flush failed");
    }
}
