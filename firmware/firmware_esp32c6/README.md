# brightness_sensor_esp32c6

Прошивка на `ESP-IDF` для платы `Waveshare ESP32-C6-LCD-1.47`.

Что делает прошивка:

- использует встроенный LCD `1.47"` на контроллере `ST7789`;
- читает сырое значение `ADC` с фоторезистора `KY-018`;
- показывает статус и текущее значение на экране;
- отправляет телеметрию в `USB Serial` в формате JSONL;
- совместима с Windows-приложением из `pc-app/`.

Формат телеметрии:

`{"deviceId":"esp32c6-01","sensorId":"light0","ts":123456,"value":321}`

Для этой прошивки поле `value` содержит сырое значение `ADC` в диапазоне `0..4095`.

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
cd C:\Users\Lenovo\Nextcloud\Repos\brig\brightness-sensor\firmware\firmware_esp32c6
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
cd C:\Users\Lenovo\Nextcloud\Repos\brig\brightness-sensor\firmware\firmware_esp32c6
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

### KY-018

Подключение по умолчанию:

- `VCC` -> `3V3`
- `GND` -> `GND`
- `AO` -> `GPIO0`

Важно:

- используется только аналоговый выход `AO`;
- обязательно нужен общий `GND`;
- не подавайте `5V` на датчик.

Если датчик не даёт валидные показания:

- проверьте, что `AO` подключён именно к `GPIO0`;
- проверьте питание `3V3` и общий `GND`;
- проверьте надёжность контактов на breadboard;
- если у модуля есть маркировка `S`, `+`, `-`, то `S` — это аналоговый сигнал (`AO`).

## Что должно происходить после старта

На экране:

- `ID`
- `ADC`
- `VALUE`
- `STATUS`

В monitor:

- логи старта `ESP-IDF`;
- сообщение `LCD ready`;
- при успешной инициализации датчика строка вида:

`KY-018 ready on ADC unit 1, channel 0, gpio=0`

Если инициализация или чтение не удались, прошивка пишет `sensor_ky018_* failed`.

## Подключение к Windows-приложению

Для `pc-app` используйте пример:

- [pc-app/appsettings.esp32c6.example.json](/C:/Users/Lenovo/Nextcloud/Repos/brig/brightness-sensor/pc-app/appsettings.esp32c6.example.json)

Важно:

- `serial.deviceId` должен совпадать с `APP_DEVICE_ID` в `main/app_config.h`;
- по умолчанию это `esp32c6-01`;
- `baudRate` должен быть `115200`.

## Настройки проекта

Основные константы лежат в:

- [main/app_config.h](/C:/Users/Lenovo/Nextcloud/Repos/brig/brightness-sensor/firmware/firmware_esp32c6/main/app_config.h)

Там можно менять:

- `APP_DEVICE_ID`
- `APP_SENSOR_ID`
- интервалы обновления
- `APP_KY018_ADC_CHANNEL`
- `APP_KY018_ADC_GPIO`
- LCD-пины и размеры

Если вы меняете `APP_DEVICE_ID`, не забудьте обновить `serial.deviceId` в `pc-app/appsettings.json`.
