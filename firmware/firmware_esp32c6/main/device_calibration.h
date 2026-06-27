#pragma once

#include <stdbool.h>

typedef struct {
    int adc_min;
    int adc_max;
    bool invert;
    float gamma;
    float normalized_offset;
    bool calibrated;
} device_calibration_t;

void device_calibration_init(device_calibration_t *calibration, int adc_min, int adc_max, bool invert, float gamma);
bool device_calibration_try_calibrate(
    device_calibration_t *calibration,
    int raw_adc_value,
    int screen_brightness_percent,
    char *error_text,
    int error_text_size);
bool device_calibration_apply(const device_calibration_t *calibration, int raw_adc_value, int *normalized_value_1000);
float device_calibration_get_offset(const device_calibration_t *calibration);
