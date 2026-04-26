#include "sensor_bh1750.h"

#include <math.h>
#include <stddef.h>
#include <stdint.h>

#include "app_config.h"
#include "driver/i2c.h"
#include "esp_check.h"
#include "esp_log.h"
#include "freertos/FreeRTOS.h"

static const char *TAG = "sensor_bh1750";
static bool s_i2c_driver_installed;

enum {
    BH1750_POWER_ON = 0x01,
    BH1750_RESET = 0x07,
    BH1750_CONT_H_RES_MODE = 0x10,
};

static esp_err_t bh1750_write_byte(uint8_t address, uint8_t value)
{
    return i2c_master_write_to_device(
        APP_BH1750_I2C_PORT,
        address,
        &value,
        sizeof(value),
        pdMS_TO_TICKS(100));
}

static esp_err_t i2c_probe_address(uint8_t address)
{
    i2c_cmd_handle_t cmd = i2c_cmd_link_create();
    if (cmd == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_err_t err = i2c_master_start(cmd);
    if (err == ESP_OK) {
        err = i2c_master_write_byte(cmd, (address << 1) | I2C_MASTER_WRITE, true);
    }
    if (err == ESP_OK) {
        err = i2c_master_stop(cmd);
    }
    if (err == ESP_OK) {
        err = i2c_master_cmd_begin(APP_BH1750_I2C_PORT, cmd, pdMS_TO_TICKS(50));
    }

    i2c_cmd_link_delete(cmd);
    return err;
}

esp_err_t sensor_bh1750_init(sensor_bh1750_t *sensor)
{
    ESP_RETURN_ON_FALSE(sensor != NULL, ESP_ERR_INVALID_ARG, TAG, "sensor pointer is null");

    const i2c_config_t i2c_config = {
        .mode = I2C_MODE_MASTER,
        .sda_io_num = APP_BH1750_I2C_SDA,
        .scl_io_num = APP_BH1750_I2C_SCL,
        .sda_pullup_en = GPIO_PULLUP_ENABLE,
        .scl_pullup_en = GPIO_PULLUP_ENABLE,
        .master.clk_speed = APP_BH1750_I2C_FREQ_HZ,
        .clk_flags = 0,
    };

    ESP_RETURN_ON_ERROR(i2c_param_config(APP_BH1750_I2C_PORT, &i2c_config), TAG, "i2c_param_config failed");

    if (!s_i2c_driver_installed) {
        esp_err_t err = i2c_driver_install(APP_BH1750_I2C_PORT, i2c_config.mode, 0, 0, 0);
        if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
            return err;
        }
        s_i2c_driver_installed = true;
    }

    const uint8_t possible_addresses[] = {APP_BH1750_ADDR, 0x5C};
    esp_err_t init_err = ESP_FAIL;

    for (size_t i = 0; i < sizeof(possible_addresses) / sizeof(possible_addresses[0]); ++i) {
        const uint8_t address = possible_addresses[i];

        init_err = bh1750_write_byte(address, BH1750_POWER_ON);
        if (init_err != ESP_OK) {
            continue;
        }

        ESP_RETURN_ON_ERROR(bh1750_write_byte(address, BH1750_RESET), TAG, "BH1750 reset failed");
        ESP_RETURN_ON_ERROR(bh1750_write_byte(address, BH1750_CONT_H_RES_MODE), TAG, "BH1750 mode set failed");

        sensor->address = address;
        sensor->initialized = true;
        ESP_LOGI(TAG, "BH1750 ready on I2C port %d, SDA=%d, SCL=%d, addr=0x%02X",
                 APP_BH1750_I2C_PORT,
                 APP_BH1750_I2C_SDA,
                 APP_BH1750_I2C_SCL,
                 sensor->address);
        return ESP_OK;
    }

    ESP_LOGE(TAG, "BH1750 not found on SDA=%d SCL=%d, tried addresses 0x%02X and 0x5C",
             APP_BH1750_I2C_SDA,
             APP_BH1750_I2C_SCL,
             APP_BH1750_ADDR);
    return init_err;
}

esp_err_t sensor_bh1750_read(sensor_bh1750_t *sensor, device_reading_t *reading)
{
    ESP_RETURN_ON_FALSE(sensor != NULL, ESP_ERR_INVALID_ARG, TAG, "sensor pointer is null");
    ESP_RETURN_ON_FALSE(reading != NULL, ESP_ERR_INVALID_ARG, TAG, "reading pointer is null");
    ESP_RETURN_ON_FALSE(sensor->initialized, ESP_ERR_INVALID_STATE, TAG, "sensor is not initialized");

    uint8_t raw_data[2] = {0};
    ESP_RETURN_ON_ERROR(
        i2c_master_read_from_device(
            APP_BH1750_I2C_PORT,
            sensor->address,
            raw_data,
            sizeof(raw_data),
            pdMS_TO_TICKS(100)),
        TAG,
        "BH1750 read failed");

    const uint16_t raw = ((uint16_t)raw_data[0] << 8) | raw_data[1];
    const float lux = (float)raw / 1.2f;

    reading->lux = lux;
    reading->value_for_pc = (int)lroundf(lux);
    reading->valid = true;

    return ESP_OK;
}

void sensor_bh1750_scan_bus(void)
{
    ESP_LOGI(TAG, "Scanning I2C bus on SDA=%d SCL=%d...", APP_BH1750_I2C_SDA, APP_BH1750_I2C_SCL);

    bool found_any = false;
    for (uint8_t address = 1; address < 0x78; ++address) {
        esp_err_t err = i2c_probe_address(address);
        if (err == ESP_OK) {
            ESP_LOGI(TAG, "I2C device found at 0x%02X", address);
            found_any = true;
        }
    }

    if (!found_any) {
        ESP_LOGW(TAG, "No I2C devices found on the bus");
    }
}
