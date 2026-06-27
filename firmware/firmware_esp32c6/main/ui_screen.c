#include "ui_screen.h"

#include <stdio.h>
#include <string.h>

#include "display_lcd.h"
#include "esp_log.h"
#include "ui_generated_screen.h"

static const char *TAG = "ui_screen";

static const uint16_t COLOR_STATUS_OK = 0x07E0;
static const uint16_t COLOR_STATUS_BAD = 0xF800;
static const uint16_t COLOR_TEXT = 0xFFFF;

typedef struct {
    char normalized_text[16];
    char raw_text[16];
    char status_text[16];
    bool normalized_present;
    bool raw_present;
    bool status_present;
    bool calibrated;
    bool valid;
    bool status_ok;
} ui_state_t;

static ui_state_t s_ui_state = {
    .normalized_text = "UNCAL",
    .raw_text = "0",
    .status_text = "INIT",
    .normalized_present = false,
    .raw_present = false,
    .status_present = false,
    .calibrated = false,
    .valid = false,
    .status_ok = false,
};

static bool is_placeholder(const char *text, const char *placeholder)
{
    return text != NULL && placeholder != NULL && strcmp(text, placeholder) == 0;
}

static void scan_placeholders(const ui_layout_screen_t *screen)
{
    bool normalized_present = false;
    bool raw_present = false;
    bool status_present = false;

    for (size_t i = 0; i < screen->element_count; ++i) {
        const ui_layout_element_t *element = &screen->elements[i];
        if (element->kind != UI_LAYOUT_ELEMENT_TEXT) {
            continue;
        }

        if (is_placeholder(element->text.text, UI_PLACEHOLDER_NORMALIZED)) {
            normalized_present = true;
        }
        if (is_placeholder(element->text.text, UI_PLACEHOLDER_RAW)) {
            raw_present = true;
        }
        if (is_placeholder(element->text.text, UI_PLACEHOLDER_STATUS)) {
            status_present = true;
        }
    }

    s_ui_state.normalized_present = normalized_present;
    s_ui_state.raw_present = raw_present;
    s_ui_state.status_present = status_present;

    ESP_LOGI(
        TAG,
        "UI placeholders: normalized=%s raw=%s status=%s",
        normalized_present ? "yes" : "no",
        raw_present ? "yes" : "no",
        status_present ? "yes" : "no");
}

static const char *resolve_dynamic_text(const ui_layout_text_t *text_element, uint16_t *color)
{
    if (is_placeholder(text_element->text, UI_PLACEHOLDER_NORMALIZED)) {
        *color = COLOR_TEXT;
        return s_ui_state.calibrated ? s_ui_state.normalized_text : "UNCAL";
    }

    if (is_placeholder(text_element->text, UI_PLACEHOLDER_RAW)) {
        *color = COLOR_TEXT;
        return s_ui_state.valid ? s_ui_state.raw_text : "0";
    }

    if (is_placeholder(text_element->text, UI_PLACEHOLDER_STATUS)) {
        *color = s_ui_state.status_ok ? COLOR_STATUS_OK : COLOR_STATUS_BAD;
        return s_ui_state.status_text;
    }

    *color = text_element->color;
    return text_element->text;
}

static void draw_text_element(const ui_layout_text_t *text_element)
{
    uint16_t color = text_element->color;
    const char *text = resolve_dynamic_text(text_element, &color);

    switch (text_element->align) {
    case UI_LAYOUT_ALIGN_CENTER:
        display_lcd_draw_text_centered(text_element->anchor_x, text_element->y, text, color, text_element->scale);
        break;
    case UI_LAYOUT_ALIGN_RIGHT:
        display_lcd_draw_text_right(text_element->anchor_x, text_element->y, text, color, text_element->scale);
        break;
    case UI_LAYOUT_ALIGN_LEFT:
    default:
        display_lcd_draw_text(text_element->anchor_x, text_element->y, text, color, text_element->scale);
        break;
    }
}

esp_err_t ui_screen_init(void)
{
    esp_err_t err = display_lcd_init();
    if (err != ESP_OK) {
        return err;
    }

    scan_placeholders(ui_generated_screen_get());
    return ESP_OK;
}

void ui_update_normalized(int value, bool calibrated)
{
    s_ui_state.calibrated = calibrated;
    snprintf(s_ui_state.normalized_text, sizeof(s_ui_state.normalized_text), "%d", calibrated ? value : 0);
}

void ui_update_raw(int raw, bool valid)
{
    s_ui_state.valid = valid;
    snprintf(s_ui_state.raw_text, sizeof(s_ui_state.raw_text), "%d", valid ? raw : 0);
}

void ui_update_status(const char *status, bool ok)
{
    s_ui_state.status_ok = ok;
    if (status != NULL) {
        strncpy(s_ui_state.status_text, status, sizeof(s_ui_state.status_text) - 1);
        s_ui_state.status_text[sizeof(s_ui_state.status_text) - 1] = '\0';
    }
}

void ui_screen_render(void)
{
    if (!display_lcd_is_ready()) {
        return;
    }

    const ui_layout_screen_t *screen = ui_generated_screen_get();
    display_lcd_fill_screen(screen->background_color);

    for (size_t i = 0; i < screen->element_count; ++i) {
        const ui_layout_element_t *element = &screen->elements[i];
        switch (element->kind) {
        case UI_LAYOUT_ELEMENT_RECT:
            display_lcd_fill_rect(
                element->rect.x,
                element->rect.y,
                element->rect.w,
                element->rect.h,
                element->rect.color);
            break;
        case UI_LAYOUT_ELEMENT_TEXT:
            draw_text_element(&element->text);
            break;
        default:
            break;
        }
    }

    if (display_lcd_flush() != ESP_OK) {
        ESP_LOGE(TAG, "panel flush failed");
    }
}
