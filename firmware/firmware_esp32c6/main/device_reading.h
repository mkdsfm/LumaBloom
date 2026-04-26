#pragma once

#include <stdbool.h>
#include <stdint.h>

typedef struct {
    uint64_t ts_ms;
    float lux;
    int value_for_pc;
    bool valid;
} device_reading_t;
