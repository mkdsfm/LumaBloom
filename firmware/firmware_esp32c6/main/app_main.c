#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "app_config.h"
#include "device_reading.h"
#include "display_lcd.h"
#include "esp_err.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#include "sensor_ky018.h"
#include "telemetry_serial.h"

static const char *TAG = "app_main";

typedef struct {
    device_reading_t latest_reading;
    char status_text[16];
    SemaphoreHandle_t mutex;
} app_state_t;

static app_state_t s_app_state = {
    .latest_reading = {
        .ts_ms = 0,
        .raw_adc = 0,
        .value_for_pc = 0,
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

static void sensor_task(void *arg)
{
    sensor_ky018_t *sensor = (sensor_ky018_t *)arg;

    while (true) {
        device_reading_t reading = {
            .ts_ms = now_ms(),
            .raw_adc = 0,
            .value_for_pc = 0,
            .valid = false,
        };

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
            app_state_write(&reading, "OK");
            telemetry_serial_publish(APP_DEVICE_ID, APP_SENSOR_ID, &reading);
        } else {
            ESP_LOGE(TAG, "sensor_ky018_read failed: %s", esp_err_to_name(err));
            sensor->initialized = false;
            app_state_write(&reading, "SENSOR ERR");
        }

        vTaskDelay(pdMS_TO_TICKS(APP_READ_INTERVAL_MS));
    }
}

static void display_task(void *arg)
{
    (void)arg;

    while (true) {
        device_reading_t reading;
        char status_text[16];

        app_state_read(&reading, status_text, sizeof(status_text));
        display_lcd_render(APP_DEVICE_ID, &reading, status_text);

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

    esp_err_t display_err = display_lcd_init();
    if (display_err != ESP_OK) {
        ESP_LOGE(TAG, "display_lcd_init failed: %s", esp_err_to_name(display_err));
    } else {
        display_lcd_render(APP_DEVICE_ID, &s_app_state.latest_reading, s_app_state.status_text);
    }

    static sensor_ky018_t sensor = {0};
    esp_err_t sensor_err = sensor_ky018_init(&sensor);
    if (sensor_err != ESP_OK) {
        ESP_LOGE(TAG, "sensor_ky018_init failed: %s", esp_err_to_name(sensor_err));
        strncpy(s_app_state.status_text, "INIT ERR", sizeof(s_app_state.status_text) - 1);
        s_app_state.status_text[sizeof(s_app_state.status_text) - 1] = '\0';
    }

    xTaskCreate(sensor_task, "sensor_task", 4096, &sensor, 5, NULL);
    xTaskCreate(display_task, "display_task", 6144, NULL, 4, NULL);
}
