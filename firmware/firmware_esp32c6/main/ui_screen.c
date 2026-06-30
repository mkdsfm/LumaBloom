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
static const uint16_t COLOR_SECONDARY = 0x7BEF;

static const int OUTER_FRAME_X = 2;
static const int OUTER_FRAME_Y = 2;
static const int OUTER_FRAME_W = 316;
static const int OUTER_FRAME_H = 168;
static const int INNER_FRAME_X = 5;
static const int INNER_FRAME_Y = 5;
static const int INNER_FRAME_W = 310;
static const int INNER_FRAME_H = 162;

static const int SPLIT_LINE_X = 154;
static const int SPLIT_LINE_Y0 = 14;
static const int SPLIT_LINE_Y1 = 101;
static const int LOWER_LINE_X0 = 8;
static const int LOWER_LINE_Y = 104;
static const int LOWER_LINE_X1 = 312;

static const int PERCENT_X = 18;
static const int PERCENT_Y = 20;
static const int PERCENT_SCALE = 7;
static const int PERCENT_100_X = 16;
static const int PERCENT_100_Y = 22;
static const int PERCENT_100_SCALE = 6;
static const int ADC_X = 18;
static const int ADC_Y = 76;
static const int ADC_SCALE = 2;

static const int LABEL_LEFT_X = 12;
static const int LABEL_CENTER_X = 154;
static const int LABEL_RIGHT_X = 291;
static const int LABEL_Y = 132;
static const int LABEL_SCALE = 1;

static const int BAR_X = 12;
static const int BAR_Y = 112;
static const int BAR_W = 296;
static const int BAR_H = 14;
static const int BAR_INNER_X = 13;
static const int BAR_INNER_Y = 113;
static const int BAR_INNER_W = 294;
static const int BAR_INNER_H = 12;
static const int BAR_CENTER_X = 160;

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

static int ui_y_point(int y)
{
    return APP_LCD_HEIGHT - 1 - y;
}

static void ui_fill_rect(int x, int y, int w, int h, uint16_t color)
{
    display_lcd_fill_rect(x, ui_y_rect(y, h), w, h, color);
}

static void ui_draw_rect(int x, int y, int w, int h, uint16_t color)
{
    display_lcd_draw_rect(x, ui_y_rect(y, h), w, h, color);
}

static void ui_draw_line(int x0, int y0, int x1, int y1, uint16_t color)
{
    display_lcd_draw_line(x0, ui_y_point(y0), x1, ui_y_point(y1), color);
}

static void ui_draw_text(int x, int y, const char *text, uint16_t color, int scale)
{
    display_lcd_draw_text(x, ui_y_rect(y, 7 * scale), text, color, scale);
}

static void ui_draw_circle(int center_x, int center_y, int radius, uint16_t color)
{
    display_lcd_draw_circle(center_x, ui_y_point(center_y), radius, color);
}

static void draw_dashed_vertical_line(int x, int y, int height, uint16_t color)
{
    for (int offset = 0; offset < height; offset += 4) {
        int y0 = y + offset;
        int y1 = y0 + 1;
        if (y1 >= y + height) {
            y1 = y + height - 1;
        }
        ui_draw_line(x, y0, x, y1, color);
    }
}

static void draw_frame(void)
{
    ui_draw_rect(OUTER_FRAME_X, OUTER_FRAME_Y, OUTER_FRAME_W, OUTER_FRAME_H, COLOR_PRIMARY);
    ui_draw_rect(INNER_FRAME_X, INNER_FRAME_Y, INNER_FRAME_W, INNER_FRAME_H, COLOR_SECONDARY);
}

static void draw_region_lines(void)
{
    ui_draw_line(SPLIT_LINE_X, SPLIT_LINE_Y0, SPLIT_LINE_X, SPLIT_LINE_Y1, COLOR_PRIMARY);
    ui_draw_line(LOWER_LINE_X0, LOWER_LINE_Y, LOWER_LINE_X1, LOWER_LINE_Y, COLOR_PRIMARY);
}

static void draw_main_value(void)
{
    const char *text = percentage_text();

    if (s_ui_state.calibrated && s_ui_state.brightness_percent >= 100) {
        ui_draw_text(PERCENT_100_X, PERCENT_100_Y, text, COLOR_PRIMARY, PERCENT_100_SCALE);
        return;
    }

    ui_draw_text(PERCENT_X, PERCENT_Y, text, COLOR_PRIMARY, PERCENT_SCALE);
}

static void draw_adc_line(void)
{
    char buffer[16];
    adc_text(buffer, sizeof(buffer));
    ui_draw_text(ADC_X, ADC_Y, buffer, COLOR_PRIMARY, ADC_SCALE);
}

static void draw_moon_icon(void)
{
    const int center_x = 226;
    const int center_y = 56;
    const int outer_radius = 28;
    const int inner_center_x = 242;
    const int inner_center_y = 53;
    const int inner_radius = 28;

    ui_draw_circle(center_x, center_y, outer_radius, COLOR_PRIMARY);
    ui_draw_circle(center_x, center_y, outer_radius - 1, COLOR_PRIMARY);

    for (int radius = inner_radius; radius >= 0; --radius) {
        ui_draw_circle(inner_center_x, inner_center_y, radius, COLOR_BG);
    }

    ui_draw_line(276, 20, 276, 24, COLOR_PRIMARY);
    ui_draw_line(274, 22, 278, 22, COLOR_PRIMARY);

    ui_draw_line(260, 56, 260, 60, COLOR_PRIMARY);
    ui_draw_line(258, 58, 262, 58, COLOR_PRIMARY);

    ui_draw_line(280, 80, 280, 84, COLOR_PRIMARY);
    ui_draw_line(278, 82, 282, 82, COLOR_PRIMARY);
}

static void draw_sun_icon(void)
{
    const int center_x = 235;
    const int center_y = 56;
    const int inner_radius = 17;

    ui_draw_circle(center_x, center_y, inner_radius, COLOR_PRIMARY);
    ui_draw_circle(center_x, center_y, inner_radius - 1, COLOR_PRIMARY);

    ui_draw_line(center_x, center_y - 29, center_x, center_y - 23, COLOR_PRIMARY);
    ui_draw_line(center_x, center_y + 23, center_x, center_y + 29, COLOR_PRIMARY);
    ui_draw_line(center_x - 29, center_y, center_x - 23, center_y, COLOR_PRIMARY);
    ui_draw_line(center_x + 23, center_y, center_x + 29, center_y, COLOR_PRIMARY);

    ui_draw_line(center_x - 21, center_y - 21, center_x - 16, center_y - 16, COLOR_PRIMARY);
    ui_draw_line(center_x + 16, center_y - 16, center_x + 21, center_y - 21, COLOR_PRIMARY);
    ui_draw_line(center_x - 21, center_y + 21, center_x - 16, center_y + 16, COLOR_PRIMARY);
    ui_draw_line(center_x + 16, center_y + 16, center_x + 21, center_y + 21, COLOR_PRIMARY);
}

static void draw_icon(void)
{
    if (s_ui_state.brightness_percent > 50) {
        draw_sun_icon();
        return;
    }

    draw_moon_icon();
}

static void draw_progress_bar(void)
{
    const int fill_w = (BAR_INNER_W * s_ui_state.brightness_percent) / 100;

    ui_draw_rect(BAR_X, BAR_Y, BAR_W, BAR_H, COLOR_PRIMARY);
    if (fill_w > 0) {
        ui_fill_rect(BAR_INNER_X, BAR_INNER_Y, fill_w, BAR_INNER_H, COLOR_PRIMARY);
    }

    draw_dashed_vertical_line(BAR_CENTER_X, BAR_INNER_Y, BAR_INNER_H, COLOR_SECONDARY);

    ui_draw_text(LABEL_LEFT_X, LABEL_Y, "0", COLOR_PRIMARY, LABEL_SCALE);
    ui_draw_text(LABEL_CENTER_X, LABEL_Y, "50", COLOR_PRIMARY, LABEL_SCALE);
    ui_draw_text(LABEL_RIGHT_X, LABEL_Y, "100", COLOR_PRIMARY, LABEL_SCALE);
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
    draw_frame();
    draw_region_lines();
    draw_main_value();
    draw_adc_line();
    draw_icon();
    draw_progress_bar();

    if (display_lcd_flush() != ESP_OK) {
        ESP_LOGE(TAG, "panel flush failed");
    }
}
