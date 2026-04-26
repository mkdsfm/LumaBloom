# brightness_sensor_esp32c6

Прошивка на `ESP-IDF` для платы `Waveshare ESP32-C6-LCD-1.47`.

Что делает прошивка:

- использует встроенный LCD `1.47"` на контроллере `ST7789`;
- читает освещенность с `BH1750` по `I2C`;
- показывает статус и текущее значение на экране;
- отправляет телеметрию в `USB Serial` в формате JSONL;
- совместима с Windows-приложением из `pc-app/`.

Формат телеметрии:

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":123456,"value":321}`

Для этой прошивки поле `value` содержит `lux`, округлённый до `int`.

## Быстрая прошивка готовым бинарником

Если у вас уже есть готовый merged binary, прошивать удобнее всего одним файлом.

Ожидаемое имя файла:

- `brightness_sensor_esp32c6_merged.bin`

Команда прошивки:

```powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash 0x0 brightness_sensor_esp32c6_merged.bin
```

Замените `COM5` на свой порт.

После прошивки отключите и заново подключите плату либо нажмите `RST`.

## Сборка merged binary из исходников

Откройте `ESP-IDF PowerShell` и выполните:

```powershell
cd C:\Users\Lenovo\RiderProjects\brightness-sensor\firmware\firmware_esp32c6
idf.py build
mkdir .\build\release -Force
idf.py merge-bin -f raw -o build\release\brightness_sensor_esp32c6_merged.bin
```

Готовый merged binary будет лежать здесь:

- `build/release/brightness_sensor_esp32c6_merged.bin`

## Прошивка из отдельных `.bin`

После `idf.py build` появляются обычные артефакты:

- `build/bootloader/bootloader.bin`
- `build/partition_table/partition-table.bin`
- `build/brightness_sensor_esp32c6.bin`

Команда прошивки:

```powershell
esptool.py --chip esp32c6 --port COM5 --baud 460800 write-flash --flash-mode dio --flash-freq 80m --flash-size 2MB 0x0 build\bootloader\bootloader.bin 0x8000 build\partition_table\partition-table.bin 0x10000 build\brightness_sensor_esp32c6.bin
```

## Сборка и прошивка из ESP-IDF

Если вы собираете проект локально:

```powershell
cd C:\Users\Lenovo\RiderProjects\brightness-sensor\firmware\firmware_esp32c6
idf.py set-target esp32c6
idf.py build
idf.py -p COM5 flash monitor
```

## Подключение железа

### LCD на плате

Проект использует встроенный LCD Waveshare:

- `MOSI`: `GPIO6`
- `SCLK`: `GPIO7`
- `LCD_CS`: `GPIO14`
- `LCD_DC`: `GPIO15`
- `LCD_RST`: `GPIO21`
- `LCD_BL`: `GPIO22`

Контроллер дисплея: `ST7789`.

### BH1750

Подключение по умолчанию:

- `VCC` -> `3V3`
- `GND` -> `GND`
- `SDA` -> `GPIO20`
- `SCL` -> `GPIO23`
- `ADDR` -> `GND` или не подключать

Прошивка проверяет оба стандартных адреса BH1750:

- `0x23`
- `0x5C`

Если датчик не находится:

- проверьте, что `SDA` и `SCL` не перепутаны;
- проверьте общий `GND`;
- проверьте, что контакты попали в правильные ряды breadboard;
- проверьте подписи `VCC/GND/SCL/SDA` именно на вашем модуле, а не только по цветам проводов.

## Что должно происходить после старта

На экране:

- `ID`
- `LUX`
- `VALUE`
- `STATUS`

В monitor:

- логи старта `ESP-IDF`;
- сообщение `LCD ready`;
- при успешном подключении датчика строка вида:

`BH1750 ready on I2C port 0, SDA=20, SCL=23, addr=0x23`

Если датчик не найден, прошивка пишет:

- `BH1750 not found ...`
- и один раз сканирует I2C-шину.

## Подключение к Windows-приложению

Для `pc-app` используйте пример:

- [pc-app/appsettings.esp32c6.example.json](/C:/Users/Lenovo/RiderProjects/brightness-sensor/pc-app/appsettings.esp32c6.example.json)

Важно:

- `serial.deviceId` должен совпадать с `APP_DEVICE_ID` в `main/app_config.h`;
- по умолчанию это `esp32c6-01`;
- `baudRate` должен быть `115200`.

## Настройки проекта

Основные константы лежат в:

- [main/app_config.h](/C:/Users/Lenovo/RiderProjects/brightness-sensor/firmware/firmware_esp32c6/main/app_config.h)

Там можно менять:

- `APP_DEVICE_ID`
- `APP_SENSOR_ID`
- интервалы обновления
- `SDA/SCL`
- адрес по умолчанию
- LCD-пины и размеры

Если вы меняете `APP_DEVICE_ID`, не забудьте обновить `serial.deviceId` в `pc-app/appsettings.json`.
