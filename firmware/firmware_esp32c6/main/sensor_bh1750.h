#pragma once

#include <stdbool.h>

#include "device_reading.h"
#include "esp_err.h"

typedef struct {
    bool initialized;
    uint8_t address;
} sensor_bh1750_t;

esp_err_t sensor_bh1750_init(sensor_bh1750_t *sensor);
esp_err_t sensor_bh1750_read(sensor_bh1750_t *sensor, device_reading_t *reading);
void sensor_bh1750_scan_bus(void);
