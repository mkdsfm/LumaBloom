#include "serial_command.h"

#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static const char *skip_spaces(const char *text)
{
    while (text != NULL && *text != '\0' && isspace((unsigned char)*text)) {
        ++text;
    }
    return text;
}

static bool extract_int_field(const char *line, const char *field_name, int *value)
{
    if (line == NULL || field_name == NULL || value == NULL) {
        return false;
    }

    const char *field = strstr(line, field_name);
    if (field == NULL) {
        return false;
    }

    const char *colon = strchr(field, ':');
    if (colon == NULL) {
        return false;
    }

    char *end_ptr = NULL;
    long parsed_value = strtol(skip_spaces(colon + 1), &end_ptr, 10);
    if (end_ptr == NULL || end_ptr == colon + 1) {
        return false;
    }

    *value = (int)parsed_value;
    return true;
}

bool serial_command_try_parse_calibration(const char *line, calibration_command_t *command, char *error_text, int error_text_size)
{
    if (line == NULL || command == NULL) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "command payload is missing");
        }
        return false;
    }

    if (strstr(line, "\"type\"") == NULL || strstr(line, "\"calibrate\"") == NULL) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "unsupported command type");
        }
        return false;
    }

    if (!extract_int_field(line, "\"screenBrightnessPercent\"", &command->screen_brightness_percent) ||
        !extract_int_field(line, "\"sensorAverageRaw\"", &command->sensor_average_raw)) {
        if (error_text != NULL && error_text_size > 0) {
            snprintf(error_text, (size_t)error_text_size, "screenBrightnessPercent and sensorAverageRaw are required");
        }
        return false;
    }

    if (error_text != NULL && error_text_size > 0) {
        error_text[0] = '\0';
    }
    return true;
}
