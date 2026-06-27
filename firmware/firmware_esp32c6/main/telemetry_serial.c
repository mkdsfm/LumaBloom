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
        "{\"deviceId\":\"%s\",\"sensorId\":\"%s\",\"ts\":%" PRIu64 ",\"value\":%d,\"raw\":%d,\"calibrated\":%s}\n",
        device_id,
        sensor_id,
        reading->ts_ms,
        reading->value_for_pc,
        reading->raw_adc,
        reading->calibrated ? "true" : "false");
    fflush(stdout);
}

void telemetry_serial_publish_calibration_result(bool success, bool calibrated, float normalized_offset, const char *message)
{
    printf(
        "{\"type\":\"calibrationResult\",\"success\":%s,\"calibrated\":%s,\"normalizedOffset\":%.6f,\"message\":\"%s\"}\n",
        success ? "true" : "false",
        calibrated ? "true" : "false",
        (double)normalized_offset,
        message != NULL ? message : "");
    fflush(stdout);
}
