# firmware_esp32c3

Arduino-прошивка для `ESP32-C3` с аналоговым датчиком освещения `KY-018`.

Файл прошивки:

- [firmware_esp32c3.ino](/C:/Users/Lenovo/RiderProjects/brightness-sensor/firmware/firmware_esp32c3/firmware_esp32c3.ino)

Что делает прошивка:

- читает `KY-018` через `ADC` на `GPIO0`;
- раз в `500 мс` отправляет измерение в `USB Serial`;
- совместима с Windows-приложением из `pc-app/`.

Формат телеметрии:

`{"deviceId":"esp32c3-01","sensorId":"light0","ts":123456,"value":1872}`

Для этой прошивки поле `value` содержит сырое значение `ADC` в диапазоне `0..4095`.

## Подключение

- `KY-018 VCC` -> `3V3`
- `KY-018 GND` -> `GND`
- `KY-018 AO` -> `GPIO0`

Важно:

- используйте только `AO`, а не цифровой выход;
- обязательно общий `GND`;
- не подавайте `5V` на датчик.

## Быстрая прошивка через Arduino IDE

1. Откройте [firmware_esp32c3.ino](/C:/Users/Lenovo/RiderProjects/brightness-sensor/firmware/firmware_esp32c3/firmware_esp32c3.ino) в Arduino IDE.
2. Установите пакет плат `esp32` от Espressif Systems.
3. Выберите плату `ESP32-C3`.
4. Выберите COM-порт устройства.
5. При необходимости включите `USB CDC On Boot`.
6. Нажмите `Upload`.

После прошивки откройте `Serial Monitor` на скорости `115200`.

## Сборка через Arduino CLI

Официальная документация:

- [arduino-cli compile](https://arduino.github.io/arduino-cli/0.34/commands/arduino-cli_compile/)
- [Arduino CLI configuration](https://docs.arduino.cc/arduino-cli/configuration/)

### Установить core Espressif

```powershell
arduino-cli core update-index
arduino-cli core install esp32:esp32
```

### Собрать бинарники

```powershell
arduino-cli compile --fqbn esp32:esp32:esp32c3 --output-dir C:\Users\Lenovo\RiderProjects\brightness-sensor\firmware\firmware_esp32c3_build C:\Users\Lenovo\RiderProjects\brightness-sensor\firmware\firmware_esp32c3
```

После сборки бинарники будут лежать в:

- `firmware/firmware_esp32c3_build/`

Обычно там будут:

- основной `.ino.bin`
- `bootloader.bin`
- `partitions.bin`

### Собрать и сразу прошить

```powershell
arduino-cli compile --fqbn esp32:esp32:esp32c3 --upload -p COM5 C:\Users\Lenovo\RiderProjects\brightness-sensor\firmware\firmware_esp32c3
```

Замените `COM5` на свой порт.

## Бинарники для релиза

Для релиза удобно публиковать:

- основной app binary
- `bootloader.bin`
- `partitions.bin`
- короткую инструкцию по прошивке

Если вы собираете через `arduino-cli --output-dir`, забирайте файлы из:

- `firmware/firmware_esp32c3_build/`

## Прошивка готовых `.bin` через esptool

Точные имена файлов зависят от версии Arduino core, но типовая схема для `ESP32-C3` такая:

```powershell
esptool.py --chip esp32c3 --port COM5 --baud 460800 write-flash 0x0 bootloader.bin 0x8000 partitions.bin 0x10000 firmware_esp32c3.ino.bin
```

Перед публикацией проверьте точные имена файлов в папке сборки.

## Настройки прошивки

Основные параметры лежат прямо в [firmware_esp32c3.ino](/C:/Users/Lenovo/RiderProjects/brightness-sensor/firmware/firmware_esp32c3/firmware_esp32c3.ino):

- `kLightSensorPin`
- `kReadIntervalMs`
- `kDeviceId`
- `kSensorId`

Важно:

- `kDeviceId` должен совпадать с `serial.deviceId` в `pc-app/appsettings.json`;
- по умолчанию это `esp32c3-01`;
- скорость порта должна быть `115200`.

## Проверка после прошивки

Ожидаемый вывод в Serial Monitor:

```json
{"deviceId":"esp32c3-01","sensorId":"light0","ts":123456,"value":1872}
```

Если строк нет:

- проверьте COM-порт;
- проверьте скорость `115200`;
- проверьте, что включён `USB CDC On Boot`, если это требуется вашей плате;
- проверьте питание и подключение `KY-018`.
