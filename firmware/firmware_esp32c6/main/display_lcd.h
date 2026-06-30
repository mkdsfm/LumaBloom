#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "esp_err.h"

esp_err_t display_lcd_init(void);
bool display_lcd_is_ready(void);
void display_lcd_fill_screen(uint16_t color);
void display_lcd_fill_rect(int x, int y, int w, int h, uint16_t color);
void display_lcd_draw_rect(int x, int y, int w, int h, uint16_t color);
void display_lcd_draw_line(int x0, int y0, int x1, int y1, uint16_t color);
void display_lcd_draw_circle(int center_x, int center_y, int radius, uint16_t color);
void display_lcd_draw_text(int x, int y, const char *text, uint16_t color, int scale);
void display_lcd_draw_text_centered(int center_x, int y, const char *text, uint16_t color, int scale);
void display_lcd_draw_text_right(int right_x, int y, const char *text, uint16_t color, int scale);
int display_lcd_text_width(const char *text, int scale);
esp_err_t display_lcd_flush(void);
