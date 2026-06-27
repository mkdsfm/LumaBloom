#pragma once

#include <stdbool.h>

#include "esp_err.h"

esp_err_t ui_screen_init(void);
void ui_update_normalized(int value, bool calibrated);
void ui_update_raw(int raw, bool valid);
void ui_update_status(const char *status, bool ok);
void ui_screen_render(void);
