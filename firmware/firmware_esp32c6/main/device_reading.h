#pragma once

#include <stdbool.h>
#include <stdint.h>

typedef struct {
    uint64_t ts_ms;
    int raw_adc;
    int normalized_value_1000;
    bool valid;
} device_reading_t;
