#pragma once

#include <stdbool.h>

#include "device_reading.h"
#include "esp_err.h"
#include "esp_adc/adc_oneshot.h"

typedef struct {
    bool initialized;
    adc_oneshot_unit_handle_t adc_handle;
} sensor_ky018_t;

esp_err_t sensor_ky018_init(sensor_ky018_t *sensor);
esp_err_t sensor_ky018_read(sensor_ky018_t *sensor, device_reading_t *reading);
