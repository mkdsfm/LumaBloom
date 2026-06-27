#include "display_lcd.h"

#include <stdbool.h>
#include <stdint.h>
#include <string.h>

#include "app_config.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_check.h"
#include "esp_heap_caps.h"
#include "esp_lcd_panel_io.h"
#include "esp_lcd_panel_ops.h"
#include "esp_lcd_panel_vendor.h"
#include "esp_log.h"

static const char *TAG = "display_lcd";

typedef struct {
    esp_lcd_panel_io_handle_t io_handle;
    esp_lcd_panel_handle_t panel_handle;
    uint16_t *framebuffer;
    bool ready;
} lcd_state_t;

static lcd_state_t s_lcd;

typedef struct {
    char c;
    uint8_t columns[5];
} glyph_t;

static const glyph_t kGlyphs[] = {
    {' ', {0x00, 0x00, 0x00, 0x00, 0x00}},
    {'-', {0x08, 0x08, 0x08, 0x08, 0x08}},
    {'.', {0x00, 0x00, 0x00, 0x18, 0x18}},
    {'/', {0x03, 0x04, 0x08, 0x10, 0x60}},
    {':', {0x00, 0x18, 0x18, 0x00, 0x18}},
    {'0', {0x3E, 0x45, 0x49, 0x51, 0x3E}},
    {'1', {0x00, 0x21, 0x7F, 0x01, 0x00}},
    {'2', {0x23, 0x45, 0x49, 0x51, 0x21}},
    {'3', {0x42, 0x41, 0x51, 0x69, 0x46}},
    {'4', {0x0C, 0x14, 0x24, 0x7F, 0x04}},
    {'5', {0x72, 0x51, 0x51, 0x51, 0x4E}},
    {'6', {0x1E, 0x29, 0x49, 0x49, 0x06}},
    {'7', {0x40, 0x47, 0x48, 0x50, 0x60}},
    {'8', {0x36, 0x49, 0x49, 0x49, 0x36}},
    {'9', {0x30, 0x49, 0x49, 0x4A, 0x3C}},
    {'A', {0x1F, 0x24, 0x44, 0x24, 0x1F}},
    {'B', {0x7F, 0x49, 0x49, 0x49, 0x36}},
    {'C', {0x3E, 0x41, 0x41, 0x41, 0x22}},
    {'D', {0x7F, 0x41, 0x41, 0x22, 0x1C}},
    {'E', {0x7F, 0x49, 0x49, 0x49, 0x41}},
    {'H', {0x7F, 0x08, 0x08, 0x08, 0x7F}},
    {'I', {0x00, 0x41, 0x7F, 0x41, 0x00}},
    {'J', {0x02, 0x01, 0x01, 0x01, 0x7E}},
    {'K', {0x7F, 0x08, 0x14, 0x22, 0x41}},
    {'L', {0x7F, 0x01, 0x01, 0x01, 0x01}},
    {'M', {0x7F, 0x20, 0x10, 0x20, 0x7F}},
    {'N', {0x7F, 0x10, 0x08, 0x04, 0x7F}},
    {'O', {0x3E, 0x41, 0x41, 0x41, 0x3E}},
    {'P', {0x7F, 0x48, 0x48, 0x48, 0x30}},
    {'R', {0x7F, 0x48, 0x4C, 0x4A, 0x31}},
    {'S', {0x31, 0x49, 0x49, 0x49, 0x46}},
    {'T', {0x40, 0x40, 0x7F, 0x40, 0x40}},
    {'U', {0x7E, 0x01, 0x01, 0x01, 0x7E}},
    {'V', {0x7C, 0x02, 0x01, 0x02, 0x7C}},
    {'W', {0x7F, 0x02, 0x04, 0x02, 0x7F}},
    {'X', {0x63, 0x14, 0x08, 0x14, 0x63}},
    {'Y', {0x70, 0x08, 0x07, 0x08, 0x70}},
};

static int text_width(const char *text, int scale)
{
    if (text == NULL) {
        return 0;
    }
    return (int)strlen(text) * 6 * scale;
}

static const uint8_t *find_glyph(char c)
{
    for (size_t i = 0; i < sizeof(kGlyphs) / sizeof(kGlyphs[0]); ++i) {
        if (kGlyphs[i].c == c) {
            return kGlyphs[i].columns;
        }
    }
    return kGlyphs[0].columns;
}

static void set_pixel(int x, int y, uint16_t color)
{
    if (x < 0 || x >= APP_LCD_WIDTH || y < 0 || y >= APP_LCD_HEIGHT) {
        return;
    }
    s_lcd.framebuffer[(y * APP_LCD_WIDTH) + x] = color;
}

void display_lcd_fill_screen(uint16_t color)
{
    const size_t pixels = APP_LCD_WIDTH * APP_LCD_HEIGHT;
    for (size_t i = 0; i < pixels; ++i) {
        s_lcd.framebuffer[i] = color;
    }
}

void display_lcd_fill_rect(int x, int y, int w, int h, uint16_t color)
{
    for (int py = 0; py < h; ++py) {
        for (int px = 0; px < w; ++px) {
            set_pixel(x + px, y + py, color);
        }
    }
}

static void draw_char(int x, int y, char c, uint16_t color, int scale)
{
    const uint8_t *glyph = find_glyph(c);
    for (int col = 0; col < 5; ++col) {
        uint8_t bits = glyph[col];
        for (int row = 0; row < 7; ++row) {
            if (bits & (1U << row)) {
                display_lcd_fill_rect(x + (col * scale), y + (row * scale), scale, scale, color);
            }
        }
    }
}

void display_lcd_draw_text(int x, int y, const char *text, uint16_t color, int scale)
{
    if (text == NULL) {
        return;
    }

    int cursor_x = x;
    for (size_t i = 0; text[i] != '\0'; ++i) {
        draw_char(cursor_x, y, text[i], color, scale);
        cursor_x += 6 * scale;
    }
}

void display_lcd_draw_text_centered(int center_x, int y, const char *text, uint16_t color, int scale)
{
    display_lcd_draw_text(center_x - (text_width(text, scale) / 2), y, text, color, scale);
}

void display_lcd_draw_text_right(int right_x, int y, const char *text, uint16_t color, int scale)
{
    display_lcd_draw_text(right_x - text_width(text, scale), y, text, color, scale);
}

esp_err_t display_lcd_flush(void)
{
    return esp_lcd_panel_draw_bitmap(
        s_lcd.panel_handle,
        0,
        0,
        APP_LCD_WIDTH,
        APP_LCD_HEIGHT,
        s_lcd.framebuffer);
}

esp_err_t display_lcd_init(void)
{
    s_lcd.framebuffer = heap_caps_calloc(
        APP_LCD_WIDTH * APP_LCD_HEIGHT,
        sizeof(uint16_t),
        MALLOC_CAP_8BIT);
    ESP_RETURN_ON_FALSE(s_lcd.framebuffer != NULL, ESP_ERR_NO_MEM, TAG, "framebuffer allocation failed");

    const spi_bus_config_t bus_config = {
        .sclk_io_num = APP_LCD_SPI_CLK,
        .mosi_io_num = APP_LCD_SPI_MOSI,
        .miso_io_num = GPIO_NUM_NC,
        .quadwp_io_num = GPIO_NUM_NC,
        .quadhd_io_num = GPIO_NUM_NC,
        .max_transfer_sz = APP_LCD_WIDTH * APP_LCD_HEIGHT * sizeof(uint16_t),
    };
    ESP_RETURN_ON_ERROR(spi_bus_initialize(APP_LCD_HOST, &bus_config, SPI_DMA_CH_AUTO), TAG, "spi_bus_initialize failed");

    const esp_lcd_panel_io_spi_config_t io_config = {
        .dc_gpio_num = APP_LCD_DC,
        .cs_gpio_num = APP_LCD_CS,
        .pclk_hz = APP_LCD_PIXEL_CLOCK_HZ,
        .lcd_cmd_bits = 8,
        .lcd_param_bits = 8,
        .spi_mode = 0,
        .trans_queue_depth = 10,
    };
    ESP_RETURN_ON_ERROR(
        esp_lcd_new_panel_io_spi((esp_lcd_spi_bus_handle_t)APP_LCD_HOST, &io_config, &s_lcd.io_handle),
        TAG,
        "esp_lcd_new_panel_io_spi failed");

    const esp_lcd_panel_dev_config_t panel_config = {
        .reset_gpio_num = APP_LCD_RST,
        .bits_per_pixel = 16,
        .rgb_ele_order = LCD_RGB_ELEMENT_ORDER_BGR,
    };
    ESP_RETURN_ON_ERROR(esp_lcd_new_panel_st7789(s_lcd.io_handle, &panel_config, &s_lcd.panel_handle), TAG, "esp_lcd_new_panel_st7789 failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_reset(s_lcd.panel_handle), TAG, "panel reset failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_init(s_lcd.panel_handle), TAG, "panel init failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_swap_xy(s_lcd.panel_handle, true), TAG, "panel swap_xy failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_set_gap(s_lcd.panel_handle, APP_LCD_X_OFFSET, APP_LCD_Y_OFFSET), TAG, "panel set gap failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_mirror(s_lcd.panel_handle, true, true), TAG, "panel mirror failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_invert_color(s_lcd.panel_handle, true), TAG, "panel invert color failed");
    ESP_RETURN_ON_ERROR(esp_lcd_panel_disp_on_off(s_lcd.panel_handle, true), TAG, "panel on failed");

    gpio_config_t backlight_config = {
        .pin_bit_mask = 1ULL << APP_LCD_BL,
        .mode = GPIO_MODE_OUTPUT,
    };
    ESP_RETURN_ON_ERROR(gpio_config(&backlight_config), TAG, "backlight gpio_config failed");
    ESP_RETURN_ON_ERROR(gpio_set_level(APP_LCD_BL, 1), TAG, "backlight enable failed");

    s_lcd.ready = true;
    ESP_LOGI(TAG, "LCD ready (%dx%d, x_offset=%d, y_offset=%d)",
             APP_LCD_WIDTH, APP_LCD_HEIGHT, APP_LCD_X_OFFSET, APP_LCD_Y_OFFSET);
    return ESP_OK;
}

bool display_lcd_is_ready(void)
{
    return s_lcd.ready;
}
