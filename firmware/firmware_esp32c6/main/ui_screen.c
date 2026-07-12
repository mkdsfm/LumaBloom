#include "ui_screen.h"

#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#include "app_config.h"
#include "display_lcd.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "flower_sprite_asset.h"

static const char *TAG = "ui_screen";

// Logical BGR565 colors. display_lcd.c swaps their bytes before SPI transfer.
// Keep these aligned with colors from the sprite palette when changing artwork.
static const uint16_t COLOR_TEXT = 0x765F;
static const uint16_t COLOR_TEXT_OUTLINE = 0x2881;

// Overlay layout knobs. TOP values are measured down from the visible top edge,
// while X values are measured from the visible left edge.
//
// Font geometry is 5x7 pixels with a 1-pixel column gap. Therefore a string is
// approximately `characters * 6 * scale` pixels wide and `7 * scale` high.
// Move or resize the percentage here without changing the rendering logic.
static const int PERCENT_X = 58;
static const int PERCENT_TOP = 40;
static const int PERCENT_SCALE = 4;
static const int PERCENT_HEIGHT = 7 * PERCENT_SCALE;

// ADC line layout. Keep ADC_TOP below PERCENT_TOP + PERCENT_HEIGHT so the two
// outlined strings do not overlap.
static const int ADC_X = 58;
static const int ADC_TOP = 76;
static const int ADC_SCALE = 2;
static const int ADC_HEIGHT = 7 * ADC_SCALE;

typedef struct {
    int brightness_percent;
    int adc_raw;
    int current_frame;
    int target_frame;
    bool has_valid_reading;
    bool sensor_error;
    bool dirty;
    int64_t last_animation_step_ms;
} ui_state_t;

static ui_state_t s_ui_state = {
    .brightness_percent = 0,
    .adc_raw = 0,
    .current_frame = 0,
    .target_frame = 0,
    .has_valid_reading = false,
    .sensor_error = false,
    .dirty = true,
    .last_animation_step_ms = 0,
};

static int64_t now_ms(void)
{
    return esp_timer_get_time() / 1000;
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

static int percent_to_frame(int percent)
{
    // Divide the inclusive 0..100 range into nine nearly equal ranges.
    // This guarantees frame 0 at 0% and frame 8 at 100%.
    return (clamp_percent(percent) * FLOWER_SPRITE_FRAME_COUNT) / 101;
}

static int frame_lower_bound(int frame)
{
    if (frame <= 0) {
        return 0;
    }
    return ((frame * 101) + FLOWER_SPRITE_FRAME_COUNT - 1) / FLOWER_SPRITE_FRAME_COUNT;
}

static void update_target_frame(int percent)
{
    int candidate = percent_to_frame(percent);
    if (!s_ui_state.has_valid_reading || percent == 0 || percent == 100) {
        s_ui_state.target_frame = candidate;
        return;
    }

    if (candidate > s_ui_state.target_frame) {
        // Require light to pass the next boundary by the hysteresis margin.
        int threshold = frame_lower_bound(s_ui_state.target_frame + 1) + APP_ANIMATION_HYSTERESIS_PERCENT;
        if (percent >= threshold) {
            s_ui_state.target_frame = candidate;
        }
    } else if (candidate < s_ui_state.target_frame) {
        // Use the opposite margin while closing so boundary noise is ignored.
        int threshold = frame_lower_bound(s_ui_state.target_frame) - APP_ANIMATION_HYSTERESIS_PERCENT;
        if (percent <= threshold) {
            s_ui_state.target_frame = candidate;
        }
    }
}

static int framebuffer_y_from_top(int top, int height)
{
    // The current panel mirror/orientation makes framebuffer Y grow upward.
    // UI layout is easier to edit in normal top-down screen coordinates, so
    // convert the TOP constants only at the final drawing boundary.
    return APP_LCD_HEIGHT - top - height;
}

static void percentage_text(char *buffer, size_t buffer_size)
{
    if (!s_ui_state.has_valid_reading) {
        snprintf(buffer, buffer_size, "--%%");
    } else if (s_ui_state.sensor_error) {
        snprintf(buffer, buffer_size, "ERR");
    } else {
        snprintf(buffer, buffer_size, "%d%%", s_ui_state.brightness_percent);
    }
}

static void adc_text(char *buffer, size_t buffer_size)
{
    if (!s_ui_state.has_valid_reading) {
        snprintf(buffer, buffer_size, "ADC ----");
    } else if (s_ui_state.sensor_error) {
        snprintf(buffer, buffer_size, "ADC ERR");
    } else {
        snprintf(buffer, buffer_size, "ADC %d", s_ui_state.adc_raw);
    }
}

static bool step_animation(int64_t current_time_ms)
{
    if (!s_ui_state.has_valid_reading || s_ui_state.sensor_error ||
        s_ui_state.current_frame == s_ui_state.target_frame) {
        return false;
    }

    if (current_time_ms - s_ui_state.last_animation_step_ms < APP_ANIMATION_FRAME_INTERVAL_MS) {
        return false;
    }

    // Always move by one frame. Large light changes still play every in-between
    // flower pose instead of jumping directly to the target.
    s_ui_state.current_frame += s_ui_state.target_frame > s_ui_state.current_frame ? 1 : -1;
    s_ui_state.last_animation_step_ms = current_time_ms;
    return true;
}

esp_err_t ui_screen_init(void)
{
    esp_err_t err = display_lcd_init();
    if (err == ESP_OK) {
        s_ui_state.last_animation_step_ms = now_ms();
        s_ui_state.dirty = true;
    }
    return err;
}

void ui_update_reading(int brightness_percent, int adc_raw)
{
    int clamped_percent = clamp_percent(brightness_percent);
    int clamped_adc = adc_raw < 0 ? 0 : adc_raw;
    bool changed = !s_ui_state.has_valid_reading || s_ui_state.sensor_error ||
                   s_ui_state.brightness_percent != clamped_percent ||
                   s_ui_state.adc_raw != clamped_adc;

    update_target_frame(clamped_percent);
    s_ui_state.brightness_percent = clamped_percent;
    s_ui_state.adc_raw = clamped_adc;
    s_ui_state.has_valid_reading = true;
    s_ui_state.sensor_error = false;
    s_ui_state.dirty = s_ui_state.dirty || changed;
}

void ui_update_sensor_error(void)
{
    if (s_ui_state.has_valid_reading && !s_ui_state.sensor_error) {
        s_ui_state.sensor_error = true;
        s_ui_state.dirty = true;
    }
}

void ui_screen_render(void)
{
    if (!display_lcd_is_ready()) {
        return;
    }

    bool animation_changed = step_animation(now_ms());
    if (!s_ui_state.dirty && !animation_changed) {
        return;
    }

    // Composition order matters: draw the full-screen sprite first, then place
    // text on top. Add any future UI elements between these steps and flush().
    esp_err_t draw_err = display_lcd_draw_indexed_2x(
        flower_sprite_frames[s_ui_state.current_frame],
        FLOWER_SPRITE_WIDTH,
        FLOWER_SPRITE_HEIGHT,
        flower_sprite_palette,
        FLOWER_SPRITE_PALETTE_SIZE);
    if (draw_err != ESP_OK) {
        ESP_LOGE(TAG, "sprite draw failed: %s", esp_err_to_name(draw_err));
        return;
    }

    char percent_buffer[8];
    char adc_buffer[16];
    percentage_text(percent_buffer, sizeof(percent_buffer));
    adc_text(adc_buffer, sizeof(adc_buffer));
    display_lcd_draw_text_outlined(
        PERCENT_X,
        framebuffer_y_from_top(PERCENT_TOP, PERCENT_HEIGHT),
        percent_buffer,
        COLOR_TEXT,
        COLOR_TEXT_OUTLINE,
        PERCENT_SCALE);
    display_lcd_draw_text_outlined(
        ADC_X,
        framebuffer_y_from_top(ADC_TOP, ADC_HEIGHT),
        adc_buffer,
        COLOR_TEXT,
        COLOR_TEXT_OUTLINE,
        ADC_SCALE);

    esp_err_t flush_err = display_lcd_flush();
    if (flush_err != ESP_OK) {
        ESP_LOGE(TAG, "panel flush failed: %s", esp_err_to_name(flush_err));
        return;
    }
    s_ui_state.dirty = false;
}
