#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "esp_err.h"

esp_err_t display_lcd_init(void);
bool display_lcd_is_ready(void);
void display_lcd_fill_screen(uint16_t color);
void display_lcd_fill_rect(int x, int y, int w, int h, uint16_t color);
void display_lcd_draw_text(int x, int y, const char *text, uint16_t color, int scale);
void display_lcd_draw_text_centered(int center_x, int y, const char *text, uint16_t color, int scale);
void display_lcd_draw_text_right(int right_x, int y, const char *text, uint16_t color, int scale);
esp_err_t display_lcd_flush(void);
