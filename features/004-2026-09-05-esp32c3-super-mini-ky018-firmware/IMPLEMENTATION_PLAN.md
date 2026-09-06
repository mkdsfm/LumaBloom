# План реализации: прошивка ESP32-C3 Super Mini + KY-018 без дисплея

## Статус и границы

Программная часть реализована 2026-09-06 в новом треке `firmware/firmware_esp32c3_supermini/` и связанной документации. Wire-протокол и production-код `pc-app` не изменены; тесты приложения используются как regression/contract validation. Аппаратная приёмка остаётся ожидающей физической платы и KY-018.

## Архитектурное решение

Создать независимый ESP-IDF-проект вместо расширения `firmware_esp32c6` compile-time флагами. C3-прошивка имеет один sensor task и четыре небольших модуля: конфигурация, модель reading, ADC-драйвер KY-018 и JSONL publisher. Display, shared mutable UI state, calibration command reader и mutex не нужны.

Поток данных:

```text
KY-018 AO -> GPIO4 / ADC1_CH4 -> adc_oneshot_read()
          -> device_reading_t { ts_ms, raw_adc, valid }
          -> telemetry_serial_publish()
          -> USB Serial/JTAG -> COM port -> pc-app parser/discovery
```

## Этап 1. Каркас ESP-IDF-проекта

Создать `firmware/firmware_esp32c3_supermini/`:

- `CMakeLists.txt` с `project(brightness_sensor_esp32c3_supermini)`;
- `sdkconfig.defaults` с `CONFIG_IDF_TARGET="esp32c3"`, частотой CPU по умолчанию для проекта, `CONFIG_ESP_CONSOLE_USB_SERIAL_JTAG=y`, `CONFIG_ESP_CONSOLE_UART_DEFAULT=n` и info logging;
- `main/CMakeLists.txt`, регистрирующий только `app_main.c`, `sensor_ky018.c`, `telemetry_serial.c` и зависимости `esp_adc`, `esp_timer`/соответствующий timer component;
- `main/app_config.h` с `APP_PROTOCOL_ID`, `APP_READ_INTERVAL_MS`, ADC unit/channel/GPIO, bit width и attenuation;
- `main/device_reading.h` с `uint64_t ts_ms`, `int raw_adc`, `bool valid`.

Не добавлять `sdkconfig`, build outputs, managed components и зависимости ESP32-C6. Проверить target clean build. Покрывает AC-1—AC-3, AC-29.

## Этап 2. Драйвер KY-018

Реализовать `main/sensor_ky018.h/.c` по текущему ADC oneshot pattern, но без полей нормализации:

1. `sensor_ky018_init()` валидирует указатель, идемпотентно возвращает успех для готового handle, создаёт `ADC_UNIT_1`, конфигурирует `ADC_CHANNEL_4` как `ADC_BITWIDTH_12` + `ADC_ATTEN_DB_12` и логирует GPIO/channel.
2. При частично завершившейся инициализации освобождает созданный ADC unit до возврата ошибки.
3. `sensor_ky018_read()` валидирует состояние, читает один raw result, записывает его в `device_reading_t` и только после успешного чтения ставит `valid=true`.
4. Добавить явный `sensor_ky018_deinit()` либо `sensor_ky018_reset()` для удаления существующего unit handle после read error. Sensor task вызывает его перед следующей повторной инициализацией, чтобы не оставлять stale handle и не исчерпывать ADC resources.

Конфигурация `GPIO4/ADC1_CH4` подтверждается таблицей pin functions ESP32-C3; `GPIO18/19` не затрагиваются. Покрывает AC-4—AC-6, AC-12—AC-13.

## Этап 3. JSONL publisher и runtime loop

Реализовать `main/telemetry_serial.h/.c`:

- принимать protocol id и валидный reading;
- печатать только `{"id":"%s","ts":<uint64>,"raw":<int>}\n` через `printf`;
- вызывать `fflush(stdout)` после строки;
- ничего не публиковать для null/invalid input.

Реализовать `main/app_main.c`:

1. Логировать board target, protocol id и interval.
2. Создать статический `sensor_ky018_t`; отдельная установка USB driver не нужна, потому что stdout направлен в USB Serial/JTAG через `sdkconfig.defaults`.
3. Запустить один `sensor_task` с достаточным фиксированным stack и обычным приоритетом.
4. На каждой итерации сформировать invalid reading, выполнить идемпотентную инициализацию, затем чтение.
5. Только при успешном чтении установить `ts_ms = esp_timer_get_time()/1000` и вызвать publisher.
6. При любой sensor error залогировать `esp_err_to_name`, deinit/reset sensor state и перейти к `vTaskDelay(pdMS_TO_TICKS(200))`.
7. Не создавать serial command task, calibration state, mutex, display task или sleep policy.

Если `printf` не предоставляет возвращаемое значение полного размера, записать warning без остановки sensor loop; повторная публикация происходит на следующем цикле, очередь и повтор старого reading не вводятся. Это сохраняет latest-data semantics и не создаёт backpressure. Покрывает AC-7—AC-14, AC-18, AC-28—AC-29.

## Этап 4. Release adapter

Создать `firmware/firmware_esp32c3_supermini/build_merged.py`, используя контракт и CLI текущего C6 adapter, но с одним immutable variant:

- variant id `esp32c3-supermini`;
- board id `esp32-c3-super-mini`;
- ESP-IDF target/esptool chip `esp32c3`;
- build tree `build/variants/esp32c3-supermini`;
- output `build/release/luma_bloom_esp32c3-supermini_<tag>_merged.bin`;
- manifest значения из FR-10.

Сохранить параметры `--tag`, `--variant`, `--skip-build`, `--idf-py`, `--idf-python`, `--dry-run`, `--list`. Без `--variant` строится единственный поддерживаемый вариант. Build-команда передаёт `-D PROJECT_VER=<tag>`, merge использует сгенерированный ESP-IDF `flash_args`, а очистка ограничивается C3 release directory и выбранным tag. Ошибки build/merge/manifest завершают скрипт ненулевым кодом.

Проверки:

- `python build_merged.py --list`;
- `python build_merged.py --dry-run --tag 1.2.3`;
- полный build и manifest inspection;
- `--skip-build` после полного build и негативный запуск без outputs;
- flash merged image по `0x0` и boot smoke test.

Покрывает AC-19—AC-24.

## Этап 5. Документация

Обновить только пользовательские документы, затронутые новым поддерживаемым треком:

- `README.md`: два firmware target, C3 без дисплея, обновлённая карта и How It Works без утверждения, что LCD обязателен;
- `firmware/README.md`: добавить C3 reference project;
- новый `firmware/firmware_esp32c3_supermini/README.md`: назначение, pinout, USB Serial/JTAG, build, flash, merged release, telemetry, troubleshooting;
- `docs/getting-started.md`: отдельные ветки C6 и C3 setup, ожидаемый результат без LCD для C3;
- `docs/firmware.md`: индекс двух проектов и точные команды target/chip;
- `docs/protocol.md`: перечислить C6 и C3 как producers общего raw-контракта;
- `docs/device-profiles.md`: пояснить отсутствие отдельного C3 runtime profile и необходимость подстройки raw range;
- `hardware/README.md`, `hardware/WIRING.md`, `hardware/BOM.md`, `hardware/ASSEMBLY.md`: новый hardware track, общая верхняя часть цветка и явное отсутствие отдельного C3-горшка и крепления платы;
- `CONTRIBUTING.md`: firmware validation для обоих проектов;
- `docs/skills-for-users.md`: третий release variant и пример точного C3 artifact name.

Не создавать отдельный `appsettings.esp32c3` с непроверенными ADC-границами. В документации использовать baseline `200..3200`, `invert=true` только как стартовую точку и описать измерение фактических bright/dark значений. Покрывает AC-25—AC-27.

## Этап 6. Проверка совместимости

### Автоматическая

1. C3: `idf.py set-target esp32c3`, clean `idf.py build`.
2. C3 release: dry-run, full `build_merged.py --tag <test-tag>`, manifest validation и `--skip-build`.
3. C6 regression: `python build_merged.py --dry-run --tag <test-tag>`; при доступном toolchain — builds обоих C6 variants.
4. PC contracts из `pc-app/`: `dotnet test brightness-sensor.sln`, в особенности `BrightnessSensor.DeviceReading.Tests` и `BundledFirmwareLocatorTests`/`FirmwareFlashService` tests.
5. Если текущие locator tests не покрывают `chip="esp32c3"`, добавить только тестовый fixture/кейс, не меняя production code: manifest принимается, а process arguments содержат `--chip esp32c3`.

### Аппаратная

Выполнить минимальный протокол из `ACCEPTANCE_CRITERIA.md`: flash, 30-секундный capture, bright/dark response, restart, sensor fault/recovery, auto-discovery в `pc-app` и flash merged image из Update либо эквивалентной команды `esptool`.

Если hardware validation недоступна исполнителю, feature нельзя считать полностью принятой: build и contract результаты фиксируются отдельно, а AC-6, AC-9, AC-11, AC-13—AC-14, AC-16—AC-17 и AC-23 остаются ожидающими проверки на устройстве.

## Соответствие требований владельцам реализации

| Область | Требования | Критерии | Владелец |
| --- | --- | --- | --- |
| ESP-IDF project | FR-1, FR-9 | AC-1—AC-3, AC-29 | CMake + `sdkconfig.defaults` |
| Hardware/ADC | FR-2, FR-3 | AC-4—AC-6, AC-11 | `app_config`, `sensor_ky018` |
| Runtime/telemetry | FR-4—FR-7, NFR | AC-7—AC-14, AC-28—AC-29 | `app_main`, `telemetry_serial` |
| PC compatibility | FR-8 | AC-15—AC-18 | существующие contracts/tests |
| Release | FR-10 | AC-19—AC-24 | `build_merged.py`, manifest |
| Documentation | FR-11 | AC-25—AC-27 | root/docs/hardware READMEs |

## Отклонённые варианты

- **Добавить C3 как `#ifdef` в проект C6.** Отклонено: board не использует LCD, а общая сборка связала бы независимые target, sdkconfig, dependencies и release variants.
- **Передавать процент вместо raw ADC.** Отклонено: нарушает действующий wire-контракт и переносит пользовательскую калибровку из `pc-app` в firmware.
- **Добавить новый protocol id или device profile.** Отклонено: текущая совместимость определяется `id="lumabloom"` и наличием `raw`; различение платы не нужно для runtime.
- **Использовать GPIO5/ADC2.** Отклонено: ESP-IDF документирует ограничения ADC2 oneshot на ESP32-C3; `GPIO4/ADC1_CH4` доступен и согласуется с существующей схемой KY-018.
- **Копировать calibration/serial-command код C6.** Отклонено: текущий `pc-app` не требует firmware calibration, а для устройства без UI это добавляет неиспользуемое состояние и двусторонний протокол.

## Риски и меры

- Разные клоны Super Mini могут разводить USB иначе. Поддерживаемая ревизия ограничена платами со встроенным USB Serial/JTAG на USB-разъёме; это проверяется до flash.
- Разброс KY-018 и ADC C3 может сделать baseline неточным. Протокол сохраняет raw, а документация требует hardware tuning через существующие app settings.
- Отключение USB consumer может влиять на buffering. Runtime не входит в sleep и не ждёт входных команд; проверяется закрытием и повторным открытием COM-порта.
- Добавление третьего firmware artifact может выявить неоднозначность выбора в Update UI. Текущий UI уже читает список manifests; функциональные изменения выбора не входят в scope, но AC-24 проверяет корректную передачу `chip` выбранного C3 manifest.

## Готовность

Материальных открытых вопросов нет. Этапы 1—5 и автоматическая часть этапа 6 выполнены; завершение полной приёмки требует аппаратной проверки на ESP32-C3 Super Mini с KY-018.
