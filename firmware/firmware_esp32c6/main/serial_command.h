#pragma once

#include <stdbool.h>

typedef struct {
    int screen_brightness_percent;
    int sensor_average_raw;
} calibration_command_t;

bool serial_command_try_parse_calibration(const char *line, calibration_command_t *command, char *error_text, int error_text_size);
