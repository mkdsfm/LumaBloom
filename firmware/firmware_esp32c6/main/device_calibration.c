#include "device_calibration.h"

#include <math.h>
#include <stdio.h>

static float clampf(float value, float min_value, float max_value)
{
    if (value < min_value) {
        return min_value;
    }
    if (value > max_value) {
        return max_value;
    }
    return value;
}

static float normalize_raw(const device_calibration_t *calibration, int raw_adc_value)
{
    int clamped = raw_adc_value;
    if (clamped < calibration->adc_min) {
        clamped = calibration->adc_min;
    }
    if (clamped > calibration->adc_max) {
        clamped = calibration->adc_max;
    }

    float normalized = (float)(clamped - calibration->adc_min) /
        (float)(calibration->adc_max - calibration->adc_min);
    if (calibration->invert) {
        normalized = 1.0f - normalized;
    }

    return clampf(normalized, 0.0f, 1.0f);
}

void device_calibration_init(device_calibration_t *calibration, int adc_min, int adc_max, bool invert, float gamma)
{
    if (calibration == NULL) {
        return;
    }

    calibration->adc_min = adc_min;
    calibration->adc_max = adc_max;
    calibration->invert = invert;
    calibration->gamma = gamma;
    calibration->normalized_offset = 0.0f;
    calibration->calibrated = false;
}

bool device_calibration_try_calibrate(
    device_calibration_t *calibration,
    int raw_adc_value,
    int screen_brightness_percent,
    char *error_text,
    int error_text_size)
{
    if (calibration == NULL) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "calibration pointer is null");
        }
        return false;
    }

    if (screen_brightness_percent < 0 || screen_brightness_percent > 100) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "screenBrightnessPercent must be in range 0..100");
        }
        return false;
    }

    if (calibration->adc_max <= calibration->adc_min) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "adc range must be greater than zero");
        }
        return false;
    }

    float expected_effective = (float)screen_brightness_percent / 100.0f;
    expected_effective = clampf(expected_effective, 0.0f, 1.0f);

    float expected_pre_gamma = expected_effective;
    if (calibration->gamma > 0.0f && fabsf(calibration->gamma - 1.0f) > 0.0001f) {
        expected_pre_gamma = powf(expected_effective, 1.0f / calibration->gamma);
    }

    float normalized = normalize_raw(calibration, raw_adc_value);
    calibration->normalized_offset = expected_pre_gamma - normalized;
    calibration->calibrated = true;

    if (error_text != NULL && error_text_size > 0) {
        error_text[0] = '\0';
    }
    return true;
}

bool device_calibration_apply(const device_calibration_t *calibration, int raw_adc_value, int *normalized_value_1000)
{
    if (calibration == NULL || normalized_value_1000 == NULL || !calibration->calibrated) {
        return false;
    }

    float normalized = normalize_raw(calibration, raw_adc_value);
    normalized = clampf(normalized + calibration->normalized_offset, 0.0f, 1.0f);
    *normalized_value_1000 = (int)lroundf(normalized * 1000.0f);
    return true;
}

float device_calibration_get_offset(const device_calibration_t *calibration)
{
    return calibration != NULL ? calibration->normalized_offset : 0.0f;
}
