#include "display_lcd.h"

#include <ctype.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
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

static const uint16_t COLOR_BG = 0x0841;
static const uint16_t COLOR_ACCENT = 0x07FF;
static const uint16_t COLOR_TEXT = 0xFFFF;
static const uint16_t COLOR_GOOD = 0x07E0;
static const uint16_t COLOR_BAD = 0xF800;
static const uint16_t COLOR_MUTED = 0x8410;
static const uint16_t COLOR_PANEL = 0x18C3;
static const uint16_t COLOR_PANEL_ALT = 0x10A2;

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

static void fill_screen(uint16_t color)
{
    const size_t pixels = APP_LCD_WIDTH * APP_LCD_HEIGHT;
    for (size_t i = 0; i < pixels; ++i) {
        s_lcd.framebuffer[i] = color;
    }
}

static void fill_rect(int x, int y, int w, int h, uint16_t color)
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
                fill_rect(x + (col * scale), y + (row * scale), scale, scale, color);
            }
        }
    }
}

static void draw_text(int x, int y, const char *text, uint16_t color, int scale)
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

static void draw_text_centered(int center_x, int y, const char *text, uint16_t color, int scale)
{
    draw_text(center_x - (text_width(text, scale) / 2), y, text, color, scale);
}

static void draw_text_right(int right_x, int y, const char *text, uint16_t color, int scale)
{
    draw_text(right_x - text_width(text, scale), y, text, color, scale);
}

static void to_uppercase_copy(const char *source, char *dest, size_t dest_size)
{
    if (dest_size == 0) {
        return;
    }

    size_t i = 0;
    for (; source != NULL && source[i] != '\0' && i + 1 < dest_size; ++i) {
        dest[i] = (char)toupper((unsigned char)source[i]);
    }
    dest[i] = '\0';
}

static esp_err_t flush_to_panel(void)
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

void display_lcd_render(const char *device_id, const device_reading_t *reading, const char *status_text)
{
    if (!s_lcd.ready || reading == NULL) {
        return;
    }

    char device_id_upper[32];
    char sensor_id_upper[16];
    char adc_value[16];
    char status_upper[16];
    char status_line[24];
    const char *resolved_status = status_text != NULL ? status_text : "UNKNOWN";
    const uint16_t status_color = reading->valid ? COLOR_GOOD : COLOR_BAD;
    const int header_height = 30;
    const int footer_height = 24;
    const int panel_margin = 8;
    const int footer_y = APP_LCD_HEIGHT - footer_height;
    const int panel_x = panel_margin;
    const int panel_y = header_height + 8;
    const int panel_w = APP_LCD_WIDTH - (panel_margin * 2);
    const int panel_h = footer_y - panel_y - 6;
    const int status_badge_w = 126;
    const int status_badge_h = 18;

    to_uppercase_copy(device_id, device_id_upper, sizeof(device_id_upper));
    to_uppercase_copy(APP_SENSOR_ID, sensor_id_upper, sizeof(sensor_id_upper));
    to_uppercase_copy(resolved_status, status_upper, sizeof(status_upper));
    snprintf(adc_value, sizeof(adc_value), "%d", reading->valid ? reading->raw_adc : 0);
    snprintf(status_line, sizeof(status_line), "STATUS %s", status_upper);

    fill_screen(COLOR_BG);
    fill_rect(0, 0, APP_LCD_WIDTH, header_height, COLOR_ACCENT);
    fill_rect(panel_x, panel_y, panel_w, panel_h, COLOR_PANEL);
    fill_rect(panel_x, panel_y, 6, panel_h, status_color);
    fill_rect(0, footer_y, APP_LCD_WIDTH, footer_height, COLOR_PANEL_ALT);
    fill_rect(APP_LCD_WIDTH - status_badge_w - 10, 6, status_badge_w, status_badge_h, status_color);

    draw_text(10, 8, "KY-018", COLOR_BG, 2);
    draw_text(90, 10, device_id_upper, COLOR_BG, 1);
    draw_text_centered(APP_LCD_WIDTH - (status_badge_w / 2) - 10, 11, status_line, COLOR_BG, 1);

    draw_text_centered(APP_LCD_WIDTH / 2, panel_y + 12, "RAW ADC", COLOR_MUTED, 2);
    draw_text_centered(APP_LCD_WIDTH / 2, panel_y + 44, adc_value, COLOR_TEXT, 7);

    draw_text(18, footer_y + 8, sensor_id_upper, COLOR_TEXT, 1);
    draw_text_centered(APP_LCD_WIDTH / 2, footer_y + 6, "USB JSONL", COLOR_MUTED, 2);
    draw_text_right(APP_LCD_WIDTH - 10, footer_y + 8, "115200 PC", COLOR_TEXT, 1);

    if (flush_to_panel() != ESP_OK) {
        ESP_LOGE(TAG, "panel flush failed");
    }
}
