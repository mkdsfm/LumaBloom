#include "sensor_ky018.h"

#include "app_config.h"
#include "esp_check.h"
#include "esp_log.h"

static const char *TAG = "sensor_ky018";

esp_err_t sensor_ky018_init(sensor_ky018_t *sensor)
{
    ESP_RETURN_ON_FALSE(sensor != NULL, ESP_ERR_INVALID_ARG, TAG, "sensor pointer is null");

    if (sensor->initialized) {
        return ESP_OK;
    }

    if (sensor->adc_handle != NULL) {
        sensor->initialized = true;
        return ESP_OK;
    }

    const adc_oneshot_unit_init_cfg_t unit_config = {
        .unit_id = APP_KY018_ADC_UNIT,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };

    adc_oneshot_unit_handle_t adc_handle = NULL;
    ESP_RETURN_ON_ERROR(adc_oneshot_new_unit(&unit_config, &adc_handle), TAG, "adc_oneshot_new_unit failed");

    const adc_oneshot_chan_cfg_t channel_config = {
        .atten = ADC_ATTEN_DB_12,
        .bitwidth = ADC_BITWIDTH_12,
    };
    esp_err_t err = adc_oneshot_config_channel(adc_handle, APP_KY018_ADC_CHANNEL, &channel_config);
    if (err != ESP_OK) {
        adc_oneshot_del_unit(adc_handle);
        return err;
    }

    sensor->adc_handle = adc_handle;
    sensor->initialized = true;

    ESP_LOGI(TAG, "KY-018 ready on ADC unit %d, channel %d, gpio=%d",
             APP_KY018_ADC_UNIT,
             APP_KY018_ADC_CHANNEL,
             APP_KY018_ADC_GPIO);
    return ESP_OK;
}

esp_err_t sensor_ky018_read(sensor_ky018_t *sensor, device_reading_t *reading)
{
    ESP_RETURN_ON_FALSE(sensor != NULL, ESP_ERR_INVALID_ARG, TAG, "sensor pointer is null");
    ESP_RETURN_ON_FALSE(reading != NULL, ESP_ERR_INVALID_ARG, TAG, "reading pointer is null");
    ESP_RETURN_ON_FALSE(sensor->initialized, ESP_ERR_INVALID_STATE, TAG, "sensor is not initialized");
    ESP_RETURN_ON_FALSE(sensor->adc_handle != NULL, ESP_ERR_INVALID_STATE, TAG, "adc handle is not initialized");

    int raw_adc = 0;
    ESP_RETURN_ON_ERROR(
        adc_oneshot_read(sensor->adc_handle, APP_KY018_ADC_CHANNEL, &raw_adc),
        TAG,
        "KY-018 read failed");

    reading->raw_adc = raw_adc;
    reading->normalized_value_1000 = 0;
    reading->value_for_pc = 0;
    reading->calibrated = false;
    reading->valid = true;
    return ESP_OK;
}
