#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "app_config.h"
#include "device_calibration.h"
#include "device_reading.h"
#include "driver/usb_serial_jtag.h"
#include "esp_err.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#include "sensor_ky018.h"
#include "serial_command.h"
#include "telemetry_serial.h"
#include "ui_screen.h"

static const char *TAG = "app_main";

typedef struct {
    device_reading_t latest_reading;
    char status_text[16];
    device_calibration_t calibration;
    SemaphoreHandle_t mutex;
} app_state_t;

static app_state_t s_app_state = {
    .latest_reading = {
        .ts_ms = 0,
        .raw_adc = 0,
        .normalized_value_1000 = 0,
        .value_for_pc = 0,
        .calibrated = false,
        .valid = false,
    },
    .status_text = "INIT",
    .mutex = NULL,
};

static uint64_t now_ms(void)
{
    return (uint64_t)(esp_timer_get_time() / 1000ULL);
}

static void app_state_write(const device_reading_t *reading, const char *status_text)
{
    if (xSemaphoreTake(s_app_state.mutex, portMAX_DELAY) == pdTRUE) {
        s_app_state.latest_reading = *reading;
        if (status_text != NULL) {
            strncpy(s_app_state.status_text, status_text, sizeof(s_app_state.status_text) - 1);
            s_app_state.status_text[sizeof(s_app_state.status_text) - 1] = '\0';
        }
        xSemaphoreGive(s_app_state.mutex);
    }
}

static void app_state_read(device_reading_t *reading, char *status_text, size_t status_text_size)
{
    if (xSemaphoreTake(s_app_state.mutex, portMAX_DELAY) == pdTRUE) {
        *reading = s_app_state.latest_reading;
        if (status_text != NULL && status_text_size > 0) {
            strncpy(status_text, s_app_state.status_text, status_text_size - 1);
            status_text[status_text_size - 1] = '\0';
        }
        xSemaphoreGive(s_app_state.mutex);
    }
}

static void app_state_apply_calibration(device_reading_t *reading, char *status_text, size_t status_text_size)
{
    if (xSemaphoreTake(s_app_state.mutex, portMAX_DELAY) == pdTRUE) {
        reading->calibrated = device_calibration_apply(
            &s_app_state.calibration,
            reading->raw_adc,
            &reading->normalized_value_1000);
        reading->value_for_pc = reading->calibrated ? reading->normalized_value_1000 : 0;

        if (status_text != NULL && status_text_size > 0) {
            const char *resolved_status = reading->calibrated ? "OK" : "UNCAL";
            strncpy(status_text, resolved_status, status_text_size - 1);
            status_text[status_text_size - 1] = '\0';
        }
        xSemaphoreGive(s_app_state.mutex);
    }
}

static void app_state_get_calibration_snapshot(bool *calibrated, float *normalized_offset)
{
    if (xSemaphoreTake(s_app_state.mutex, portMAX_DELAY) == pdTRUE) {
        if (calibrated != NULL) {
            *calibrated = s_app_state.calibration.calibrated;
        }
        if (normalized_offset != NULL) {
            *normalized_offset = device_calibration_get_offset(&s_app_state.calibration);
        }
        xSemaphoreGive(s_app_state.mutex);
    }
}

static bool app_state_try_calibrate(
    int sensor_average_raw,
    int screen_brightness_percent,
    char *error_text,
    size_t error_text_size,
    float *normalized_offset)
{
    bool success = false;

    if (xSemaphoreTake(s_app_state.mutex, portMAX_DELAY) == pdTRUE) {
        success = device_calibration_try_calibrate(
            &s_app_state.calibration,
            sensor_average_raw,
            screen_brightness_percent,
            error_text,
            (int)error_text_size);

        strncpy(
            s_app_state.status_text,
            success ? "CAL OK" : "CAL ERR",
            sizeof(s_app_state.status_text) - 1);
        s_app_state.status_text[sizeof(s_app_state.status_text) - 1] = '\0';

        if (normalized_offset != NULL) {
            *normalized_offset = device_calibration_get_offset(&s_app_state.calibration);
        }
        xSemaphoreGive(s_app_state.mutex);
    }

    return success;
}

static void sensor_task(void *arg)
{
    sensor_ky018_t *sensor = (sensor_ky018_t *)arg;

    while (true) {
        device_reading_t reading = {
            .ts_ms = now_ms(),
            .raw_adc = 0,
            .normalized_value_1000 = 0,
            .value_for_pc = 0,
            .calibrated = false,
            .valid = false,
        };
        char status_text[16] = "UNCAL";

        if (!sensor->initialized) {
            esp_err_t init_err = sensor_ky018_init(sensor);
            if (init_err != ESP_OK) {
                ESP_LOGE(TAG, "sensor_ky018_init retry failed: %s", esp_err_to_name(init_err));
                app_state_write(&reading, "INIT ERR");
                vTaskDelay(pdMS_TO_TICKS(APP_READ_INTERVAL_MS));
                continue;
            }
        }

        esp_err_t err = sensor_ky018_read(sensor, &reading);
        if (err == ESP_OK) {
            reading.ts_ms = now_ms();
            app_state_apply_calibration(&reading, status_text, sizeof(status_text));
            app_state_write(&reading, status_text);
            telemetry_serial_publish(APP_DEVICE_ID, APP_SENSOR_ID, &reading);
        } else {
            ESP_LOGE(TAG, "sensor_ky018_read failed: %s", esp_err_to_name(err));
            sensor->initialized = false;
            app_state_write(&reading, "SENSOR ERR");
        }

        vTaskDelay(pdMS_TO_TICKS(APP_READ_INTERVAL_MS));
    }
}

static void process_serial_command_line(const char *line)
{
    calibration_command_t command = {0};
    char error_text[96];
    float normalized_offset = 0.0f;
    bool calibrated = false;

    if (!serial_command_try_parse_calibration(line, &command, error_text, sizeof(error_text))) {
        app_state_get_calibration_snapshot(&calibrated, &normalized_offset);
        telemetry_serial_publish_calibration_result(false, calibrated, normalized_offset, error_text);
        return;
    }

    bool success = app_state_try_calibrate(
        command.sensor_average_raw,
        command.screen_brightness_percent,
        error_text,
        sizeof(error_text),
        &normalized_offset);
    app_state_get_calibration_snapshot(&calibrated, NULL);
    telemetry_serial_publish_calibration_result(
        success,
        calibrated,
        normalized_offset,
        success ? "calibration applied" : error_text);
}

static void serial_command_task(void *arg)
{
    (void)arg;

    char buffer[256];
    size_t buffer_length = 0;

    while (true) {
        char read_buffer[64];
        int bytes_read = usb_serial_jtag_read_bytes(
            read_buffer,
            sizeof(read_buffer),
            pdMS_TO_TICKS(50));
        if (bytes_read > 0) {
            for (int i = 0; i < bytes_read; ++i) {
                char current = read_buffer[i];
                if (current == '\r') {
                    continue;
                }

                if (current == '\n') {
                    buffer[buffer_length] = '\0';
                    if (buffer_length > 0) {
                        process_serial_command_line(buffer);
                    }
                    buffer_length = 0;
                    continue;
                }

                if (buffer_length + 1 < sizeof(buffer)) {
                    buffer[buffer_length++] = current;
                } else {
                    buffer_length = 0;
                    telemetry_serial_publish_calibration_result(
                        false,
                        false,
                        0.0f,
                        "command line too long");
                }
            }
        }

        vTaskDelay(pdMS_TO_TICKS(50));
    }
}

static void display_task(void *arg)
{
    (void)arg;

    while (true) {
        device_reading_t reading;
        char status_text[16];

        app_state_read(&reading, status_text, sizeof(status_text));
        ui_update_normalized(reading.normalized_value_1000, reading.calibrated);
        ui_update_raw(reading.raw_adc, reading.valid);
        ui_update_status(status_text, reading.valid && reading.calibrated);
        ui_screen_render();

        vTaskDelay(pdMS_TO_TICKS(APP_DISPLAY_INTERVAL_MS));
    }
}

void app_main(void)
{
    ESP_LOGI(TAG, "Starting brightness sensor firmware for ESP32-C6");
    ESP_LOGI(TAG, "DeviceId=%s SensorId=%s ReadIntervalMs=%d",
             APP_DEVICE_ID, APP_SENSOR_ID, APP_READ_INTERVAL_MS);

    s_app_state.mutex = xSemaphoreCreateMutex();
    if (s_app_state.mutex == NULL) {
        ESP_LOGE(TAG, "Failed to create app state mutex");
        return;
    }

    device_calibration_init(
        &s_app_state.calibration,
        APP_KY018_ADC_MIN,
        APP_KY018_ADC_MAX,
        APP_KY018_INVERT != 0,
        APP_KY018_GAMMA);

    if (!usb_serial_jtag_is_driver_installed()) {
        usb_serial_jtag_driver_config_t usb_serial_config = USB_SERIAL_JTAG_DRIVER_CONFIG_DEFAULT();
        esp_err_t usb_serial_err = usb_serial_jtag_driver_install(&usb_serial_config);
        if (usb_serial_err != ESP_OK) {
            ESP_LOGE(TAG, "usb_serial_jtag_driver_install failed: %s", esp_err_to_name(usb_serial_err));
            return;
        }
    }

    esp_err_t display_err = ui_screen_init();
    if (display_err != ESP_OK) {
        ESP_LOGE(TAG, "ui_screen_init failed: %s", esp_err_to_name(display_err));
    } else {
        ui_update_normalized(s_app_state.latest_reading.normalized_value_1000, s_app_state.latest_reading.calibrated);
        ui_update_raw(s_app_state.latest_reading.raw_adc, s_app_state.latest_reading.valid);
        ui_update_status(s_app_state.status_text, false);
        ui_screen_render();
    }

    static sensor_ky018_t sensor = {0};
    esp_err_t sensor_err = sensor_ky018_init(&sensor);
    if (sensor_err != ESP_OK) {
        ESP_LOGE(TAG, "sensor_ky018_init failed: %s", esp_err_to_name(sensor_err));
        strncpy(s_app_state.status_text, "INIT ERR", sizeof(s_app_state.status_text) - 1);
        s_app_state.status_text[sizeof(s_app_state.status_text) - 1] = '\0';
    }

    xTaskCreate(sensor_task, "sensor_task", 4096, &sensor, 5, NULL);
    xTaskCreate(serial_command_task, "serial_command_task", 4096, NULL, 5, NULL);
    xTaskCreate(display_task, "display_task", 6144, NULL, 4, NULL);
}
