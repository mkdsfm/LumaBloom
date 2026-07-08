#include "telemetry_serial.h"

#include <stdbool.h>
#include <inttypes.h>
#include <stdio.h>

void telemetry_serial_publish(const char *device_id, const char *sensor_id, const device_reading_t *reading)
{
    if (device_id == NULL || sensor_id == NULL || reading == NULL || !reading->valid) {
        return;
    }

    printf(
        "{\"deviceId\":\"%s\",\"sensorId\":\"%s\",\"ts\":%" PRIu64 ",\"raw\":%d}\n",
        device_id,
        sensor_id,
        reading->ts_ms,
        reading->raw_adc);
    fflush(stdout);
}

void telemetry_serial_publish_calibration_result(bool success, float normalized_offset, const char *message)
{
    printf(
        "{\"type\":\"calibrationResult\",\"success\":%s,\"normalizedOffset\":%.6f,\"message\":\"%s\"}\n",
        success ? "true" : "false",
        (double)normalized_offset,
        message != NULL ? message : "");
    fflush(stdout);
}
