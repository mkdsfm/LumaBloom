#pragma once

#include <stddef.h>
#include <stdint.h>

typedef enum {
    UI_LAYOUT_ELEMENT_RECT = 0,
    UI_LAYOUT_ELEMENT_TEXT = 1,
} ui_layout_element_kind_t;

typedef enum {
    UI_LAYOUT_ALIGN_LEFT = 0,
    UI_LAYOUT_ALIGN_CENTER = 1,
    UI_LAYOUT_ALIGN_RIGHT = 2,
} ui_layout_text_align_t;

typedef struct {
    int x;
    int y;
    int w;
    int h;
    uint16_t color;
} ui_layout_rect_t;

typedef struct {
    int anchor_x;
    int y;
    uint16_t color;
    int scale;
    ui_layout_text_align_t align;
    const char *text;
} ui_layout_text_t;

typedef struct {
    ui_layout_element_kind_t kind;
    union {
        ui_layout_rect_t rect;
        ui_layout_text_t text;
    };
} ui_layout_element_t;

typedef struct {
    uint16_t background_color;
    const ui_layout_element_t *elements;
    size_t element_count;
} ui_layout_screen_t;

extern const char *const UI_PLACEHOLDER_NORMALIZED;
extern const char *const UI_PLACEHOLDER_RAW;
extern const char *const UI_PLACEHOLDER_STATUS;

const ui_layout_screen_t *ui_generated_screen_get(void);
