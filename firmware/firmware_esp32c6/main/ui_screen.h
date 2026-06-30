#pragma once

#include <stdbool.h>

#include "esp_err.h"

esp_err_t ui_screen_init(void);
void ui_update_reading(int brightness_percent, int adc_raw, bool calibrated);
void ui_screen_render(void);
