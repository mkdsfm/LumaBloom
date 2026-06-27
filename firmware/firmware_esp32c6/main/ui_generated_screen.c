#include "ui_generated_screen.h"

static const uint16_t COLOR_BG = 0x0841;
static const uint16_t COLOR_TEXT = 0xFFFF;

const char *const UI_PLACEHOLDER_NORMALIZED = "{{NORMALIZED}}";
const char *const UI_PLACEHOLDER_RAW = "{{RAW}}";
const char *const UI_PLACEHOLDER_STATUS = "{{STATUS}}";

static const ui_layout_element_t kScreenElements[] = {
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 58, .color = COLOR_TEXT, .scale = 7, .align = UI_LAYOUT_ALIGN_CENTER, .text = "{{NORMALIZED}}"},
    },
};

static const ui_layout_screen_t kScreen = {
    .background_color = COLOR_BG,
    .elements = kScreenElements,
    .element_count = sizeof(kScreenElements) / sizeof(kScreenElements[0]),
};

const ui_layout_screen_t *ui_generated_screen_get(void)
{
    return &kScreen;
}
