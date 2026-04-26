#include "telemetry_serial.h"

#include <inttypes.h>
#include <stdio.h>

void telemetry_serial_publish(const char *device_id, const char *sensor_id, const device_reading_t *reading)
{
    if (device_id == NULL || sensor_id == NULL || reading == NULL || !reading->valid) {
        return;
    }

    printf(
        "{\"deviceId\":\"%s\",\"sensorId\":\"%s\",\"ts\":%" PRIu64 ",\"value\":%d}\n",
        device_id,
        sensor_id,
        reading->ts_ms,
        reading->value_for_pc);
    fflush(stdout);
}
