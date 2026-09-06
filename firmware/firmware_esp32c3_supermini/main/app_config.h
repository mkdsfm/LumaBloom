#pragma once

#include "driver/gpio.h"
#include "esp_adc/adc_oneshot.h"

#define APP_PROTOCOL_ID "lumabloom"
#define APP_READ_INTERVAL_MS 200

#define APP_KY018_ADC_UNIT ADC_UNIT_1
#define APP_KY018_ADC_CHANNEL ADC_CHANNEL_4
#define APP_KY018_ADC_GPIO GPIO_NUM_4
#define APP_KY018_ADC_BITWIDTH ADC_BITWIDTH_12
#define APP_KY018_ADC_ATTEN ADC_ATTEN_DB_12
