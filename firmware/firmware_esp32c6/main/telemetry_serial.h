#pragma once

#include "device_reading.h"

void telemetry_serial_publish(const char *device_id, const char *sensor_id, const device_reading_t *reading);
void telemetry_serial_publish_calibration_result(bool success, bool calibrated, float normalized_offset, const char *message);
