#include "telemetry_serial.h"

#include <inttypes.h>
#include <stdio.h>

bool telemetry_serial_publish(const char *protocol_id, const device_reading_t *reading)
{
    if (protocol_id == NULL || reading == NULL || !reading->valid) {
        return false;
    }

    int written = printf(
        "{\"id\":\"%s\",\"ts\":%" PRIu64 ",\"raw\":%d}\n",
        protocol_id,
        reading->ts_ms,
        reading->raw_adc);
    return written >= 0 && fflush(stdout) == 0;
}
