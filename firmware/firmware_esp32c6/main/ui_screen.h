#pragma once

#include <stdbool.h>

#include "esp_err.h"

esp_err_t ui_screen_init(void);
void ui_update_reading(int brightness_percent, int adc_raw);
void ui_update_sensor_error(void);
void ui_screen_render(void);
