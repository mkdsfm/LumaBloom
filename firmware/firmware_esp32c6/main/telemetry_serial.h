#pragma once

#include "device_reading.h"

void telemetry_serial_publish(const char *device_id, const char *sensor_id, const device_reading_t *reading);
