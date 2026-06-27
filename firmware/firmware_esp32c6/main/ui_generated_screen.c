#include "ui_generated_screen.h"

static const uint16_t COLOR_BG = 0x0841;
static const uint16_t COLOR_ACCENT = 0x07FF;
static const uint16_t COLOR_TEXT = 0xFFFF;
static const uint16_t COLOR_MUTED = 0x8410;
static const uint16_t COLOR_PANEL = 0x18C3;
static const uint16_t COLOR_PANEL_ALT = 0x10A2;

const char *const UI_PLACEHOLDER_NORMALIZED = "{{NORMALIZED}}";
const char *const UI_PLACEHOLDER_RAW = "{{RAW}}";
const char *const UI_PLACEHOLDER_STATUS = "{{STATUS}}";

static const ui_layout_element_t kScreenElements[] = {
    {
        .kind = UI_LAYOUT_ELEMENT_RECT,
        .rect = {.x = 0, .y = 0, .w = 320, .h = 30, .color = COLOR_ACCENT},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_RECT,
        .rect = {.x = 8, .y = 38, .w = 304, .h = 104, .color = COLOR_PANEL},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_RECT,
        .rect = {.x = 0, .y = 148, .w = 320, .h = 24, .color = COLOR_PANEL_ALT},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 10, .y = 8, .color = COLOR_BG, .scale = 2, .align = UI_LAYOUT_ALIGN_LEFT, .text = "KY-018"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 48, .color = COLOR_MUTED, .scale = 2, .align = UI_LAYOUT_ALIGN_CENTER, .text = "NORMALIZED"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 78, .color = COLOR_TEXT, .scale = 5, .align = UI_LAYOUT_ALIGN_CENTER, .text = "{{NORMALIZED}}"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 122, .color = COLOR_MUTED, .scale = 2, .align = UI_LAYOUT_ALIGN_CENTER, .text = "RAW"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 140, .color = COLOR_TEXT, .scale = 2, .align = UI_LAYOUT_ALIGN_CENTER, .text = "{{RAW}}"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 18, .y = 156, .color = COLOR_TEXT, .scale = 1, .align = UI_LAYOUT_ALIGN_LEFT, .text = "LIGHT0"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 160, .y = 154, .color = COLOR_MUTED, .scale = 2, .align = UI_LAYOUT_ALIGN_CENTER, .text = "USB JSONL"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 310, .y = 156, .color = COLOR_TEXT, .scale = 1, .align = UI_LAYOUT_ALIGN_RIGHT, .text = "115200 PC"},
    },
    {
        .kind = UI_LAYOUT_ELEMENT_TEXT,
        .text = {.anchor_x = 310, .y = 10, .color = COLOR_BG, .scale = 1, .align = UI_LAYOUT_ALIGN_RIGHT, .text = "{{STATUS}}"},
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
