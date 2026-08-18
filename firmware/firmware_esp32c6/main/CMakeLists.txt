idf_component_register(
    SRCS
        "app_main.c"
        "device_calibration.c"
        "display_lcd.c"
        "flower_sprite_asset.c"
        "sensor_ky018.c"
        "serial_command.c"
        "telemetry_serial.c"
        "ui_screen.c"
    INCLUDE_DIRS
        "."
    REQUIRES
        driver
        esp_adc
        esp_driver_ledc
        esp_driver_usb_serial_jtag
        esp_lcd
        esp_timer
)

# ST7789 is the default board.
# Override with:
# idf.py -D APP_DISPLAY_TYPE=2 build

if(DEFINED APP_DISPLAY_TYPE)
    target_compile_definitions(
        ${COMPONENT_LIB}
        PRIVATE APP_DISPLAY_TYPE=${APP_DISPLAY_TYPE}
    )
    message(STATUS "APP_DISPLAY_TYPE=${APP_DISPLAY_TYPE}")
else()
    message(STATUS "APP_DISPLAY_TYPE not specified, using default ST7789")
endif()
