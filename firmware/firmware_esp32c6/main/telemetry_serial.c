#include "telemetry_serial.h"

#include <stdbool.h>
#include <inttypes.h>
#include <stdio.h>

void telemetry_serial_publish(const char *protocol_id, const device_reading_t *reading)
{
    if (protocol_id == NULL || reading == NULL || !reading->valid) {
        return;
    }

    printf(
        "{\"id\":\"%s\",\"ts\":%" PRIu64 ",\"raw\":%d}\n",
        protocol_id,
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
