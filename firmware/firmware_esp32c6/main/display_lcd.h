#pragma once

#include "device_reading.h"
#include "esp_err.h"

esp_err_t display_lcd_init(void);
void display_lcd_render(const char *device_id, const device_reading_t *reading, const char *status_text);
