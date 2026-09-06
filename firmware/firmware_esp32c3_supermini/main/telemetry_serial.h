#pragma once

#include <stdbool.h>

#include "device_reading.h"

bool telemetry_serial_publish(const char *protocol_id, const device_reading_t *reading);
