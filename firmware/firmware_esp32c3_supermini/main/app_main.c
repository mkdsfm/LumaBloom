#include <stdbool.h>
#include <stdint.h>

#include "app_config.h"
#include "device_reading.h"
#include "esp_err.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "sensor_ky018.h"
#include "telemetry_serial.h"

static const char *TAG = "app_main";

static uint64_t now_ms(void)
{
    return (uint64_t)(esp_timer_get_time() / 1000ULL);
}

static void sensor_task(void *arg)
{
    sensor_ky018_t *sensor = (sensor_ky018_t *)arg;

    while (true) {
        device_reading_t reading = {
            .ts_ms = 0,
            .raw_adc = 0,
            .valid = false,
        };

        esp_err_t err = sensor_ky018_init(sensor);
        if (err == ESP_OK) {
            err = sensor_ky018_read(sensor, &reading);
        }

        if (err == ESP_OK) {
            reading.ts_ms = now_ms();
            if (!telemetry_serial_publish(APP_PROTOCOL_ID, &reading)) {
                ESP_LOGW(TAG, "telemetry publish failed");
            }
        } else {
            ESP_LOGE(TAG, "sensor cycle failed: %s", esp_err_to_name(err));
            esp_err_t deinit_err = sensor_ky018_deinit(sensor);
            if (deinit_err != ESP_OK) {
                ESP_LOGW(TAG, "sensor deinit failed: %s", esp_err_to_name(deinit_err));
            }
        }

        vTaskDelay(pdMS_TO_TICKS(APP_READ_INTERVAL_MS));
    }
}

void app_main(void)
{
    ESP_LOGI(TAG, "Starting brightness sensor firmware for ESP32-C3 Super Mini");
    ESP_LOGI(TAG, "ProtocolId=%s ReadIntervalMs=%d", APP_PROTOCOL_ID, APP_READ_INTERVAL_MS);

    static sensor_ky018_t sensor = {0};
    BaseType_t task_result = xTaskCreate(sensor_task, "sensor_task", 3072, &sensor, 5, NULL);
    if (task_result != pdPASS) {
        ESP_LOGE(TAG, "Failed to create sensor task");
    }
}
