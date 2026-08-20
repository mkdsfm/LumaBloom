# План реализации: выбор COM-порта при прошивке из UI

## Статус и scope

Статус: программная реализация завершена 2026-08-20; аппаратная прошивка требует отдельной проверки на ESP32-C6.

Целевой track: `pc-app`, прежде всего `BrightnessSensor.ConsoleApp` и тесты. Firmware, JSONL-протокол и конфигурационный контракт не изменяются.

## Текущее состояние репозитория

- `SerialPortDiscovery.ResolveFirstTelemetry()` перечисляет `SerialPort.GetPortNames()`, последовательно пробует порты и возвращает первый порт с валидной LumaBloom-телеметрией.
- `RuntimeStateStore` хранит только connection-порт в `_portName`; отдельного flash-port selection нет.
- `TerminalGuiDashboard` показывает `snapshot.PortName` как `Connected port` и передаёт в state только команду `FlashBundledFirmware` без параметров.
- `BrightnessApplication.ProcessFirmwareUpdateRequests()` заново читает `snapshot.PortName`, закрывает текущий reader, вызывает `FirmwareFlashService.Flash()` и затем запускает auto-discovery.
- Пока `TryConnectSensor()` ждёт телеметрию, firmware requests не обрабатываются. Это блокирует ручную прошивку в состоянии `WAITING`.
- `FirmwareFlashService` формирует строку `ProcessStartInfo.Arguments`; тестового seam для проверки полного запуска `esptool.exe` сейчас нет.

## Решения

1. Connection port и flash port становятся разными состояниями. `PortName` продолжает означать текущий telemetry connection; новые поля описывают только выбор на `Update`.
2. Доступные порты перечисляются без открытия и probe. Подтверждение LumaBloom-протокола остаётся обязанностью существующего `SerialPortDiscovery`.
3. Ручной flash-port не сохраняется в конфиге.
4. Firmware request становится параметризованным immutable request и фиксирует порт в момент клика.
5. Firmware requests обрабатываются и в connected loop, и внутри waiting/discovery loop.
6. Перед flash выполняется повторная дешёвая проверка присутствия порта.
7. Если flash-порт не совпадает с активным reader, reader не закрывается ради освобождения другого порта; processing loop на время синхронного flash всё равно приостанавливается естественным образом.

## Этап 1. Тестируемый каталог системных портов

В `BrightnessSensor.ConsoleApp/Application/` добавить небольшой boundary, например `SerialPortCatalog`, с единственной обязанностью получить доступные имена COM-портов.

Контракт результата должен различать:

- успешный непустой список;
- успешный пустой список;
- ошибку перечисления с пользовательским diagnostic message.

Реализация:

- вызывает `SerialPort.GetPortNames()`;
- читает `Name`, `Description` и `PNPDeviceID` через `Win32_PnPEntity`, не открывая serial port;
- добавляет friendly name и Espressif marker для `VID_303A`, но сохраняет `PortName` единственным значением, передаваемым в flash request;
- удаляет дубликаты через `StringComparer.OrdinalIgnoreCase`;
- сортирует тем же comparer;
- не открывает `SerialPort`;
- ловит platform/I/O/authorization исключения на application boundary и возвращает безопасный результат;
- принимает инъецируемый delegate/provider для unit-тестов без реального Windows hardware.

Не расширять `SerialPortDiscoveryResult`: telemetry discovery и UI enumeration имеют разные семантики.

Покрытие: AC-2–AC-5, AC-22, AC-32.

## Этап 2. Runtime-модель выбора и запросы

### Snapshot

Расширить `DashboardSnapshot` состоянием firmware port selection:

- `IReadOnlyList<string> FirmwarePortOptions`;
- `string? SelectedFirmwarePort`;
- `bool IsFirmwarePortManuallySelected`;
- `string? FirmwarePortListError`.

`PortName` не переименовывать и не переиспользовать: он остаётся connection-портом для Diagnostics и runtime reader.

### RuntimeStateStore

Добавить потокобезопасные операции:

- применить свежий успешный список портов;
- записать ошибку refresh;
- выбрать порт вручную только из текущих `FirmwarePortOptions`;
- синхронизировать default с auto-detected connection port при `SetConnection()`;
- очистить/перевыбрать значение, когда выбранный порт исчез.

Алгоритм применения списка:

1. Нормализовать список: distinct + stable case-insensitive sort.
2. Если действующий ручной порт присутствует — сохранить его.
3. Иначе снять manual flag.
4. Если `PortName` присутствует — выбрать его.
5. Иначе установить `SelectedFirmwarePort=null`.

Ошибка refresh очищает options и selected flash-port, сохраняет connection state и записывает diagnostic. Следующее успешное раскрытие полностью восстанавливает список.

### Request

Заменить `FirmwareUpdateActionRequest` enum на record, например `FirmwareFlashRequest(string PortName)`. `RequestBundledFirmwareFlash()` должен:

- вернуть false/не ставить запрос, если порт не выбран, firmware busy или запрос уже ожидает обработки;
- создать request с текущей строкой `SelectedFirmwarePort`;
- не читать `PortName` позднее в application layer.

Очередь может остаться, но должна иметь семантику single pending request, чтобы двойной клик не создавал два процесса.

Покрытие: AC-6–AC-14, AC-19–AC-21, AC-33.

## Этап 3. Terminal.Gui на экране Update

В `TerminalGuiDashboard`:

1. Добавить локализованную подпись и `ComboBox` для flash-port.
2. Разместить control в firmware-секции над `Flash firmware`; сдвинуть checkbox/buttons или высоту текста так, чтобы layout не пересекался.
3. При первом переходе на `RuntimeScreen.Update` выполнить refresh каталога, чтобы control сразу показывал auto-default, не требуя предварительного раскрытия.
4. На событии, которое срабатывает непосредственно перед каждым раскрытием `ComboBox`, синхронно вызвать быстрый `SerialPortCatalog` и передать результат в `RuntimeStateStore`, затем заменить source/items control.
5. После refresh установить индекс `SelectedFirmwarePort`, рассчитанный store.
6. На пользовательское изменение записать manual selection в store. Программное обновление source/index защитить флагом наподобие существующего `_isUpdatingPrereleaseCheckBox`, чтобы оно не превращалось в manual override.
7. Показывать placeholder/error для пустого списка и ошибки перечисления, не добавляя placeholder как допустимый порт.
8. Отключать `ComboBox` и `_flashFirmwareButton`, когда firmware busy. Кнопка также отключена при отсутствии firmware или выбранного порта.
9. `RequestBundledFirmwareFlash()` в UI должен только запросить flash текущего selected port; фактический immutable request создаёт store.
10. Обновить `BuildUpdateText()`: разделить `Connected port` и `Firmware port`, чтобы пользователь видел разницу.

Добавить ключи в `Localizer` для `en`, `ru`, `es`: подпись flash-порта, «Select port», «No COM ports found», ошибка refresh и сообщения об исчезнувшем порте. Существующие ключи не удалять без необходимости.

Проверить поведение клавиатуры Terminal.Gui: focus, раскрытие Enter/Space, выбор стрелками, закрытие Esc. Мышь должна использовать стандартное поведение `ComboBox`.

Обновить при необходимости `ConsoleDashboardRenderer`: redirected fallback не предоставляет интерактивный выбор, но должен однозначно показывать выбранный firmware port и безопасно сообщать, что интерактивный выбор доступен только в Terminal.Gui. Не добавлять stdin-prompt в redirected mode.

Покрытие: AC-1, AC-3–AC-10, AC-19–AC-27.

## Этап 4. Orchestration и доступность прошивки в WAITING

Рефакторинг должен устранить зависимость firmware update от наличия non-null reader.

### Единый обработчик

Выделить обработку одного `FirmwareFlashRequest` в метод, которому передаются:

- request с зафиксированным port;
- bundled firmware и flash service;
- каталог портов;
- optional/current `SerialSensorReader` либо явное состояние его отсутствия;
- state store и cancellation token.

Перед запуском:

1. Проверить bundled firmware.
2. Получить свежий список системных портов.
3. Убедиться, что `request.PortName` ещё присутствует с `OrdinalIgnoreCase`.
4. При ошибке обновить options/status/events и завершить request без `esptool`.
5. Перевести firmware snapshot в busy с именем фактического порта.
6. Если активный reader открыт на этом же порту, закрыть его и передать вызывающему loop состояние «reader отсутствует».

После `FirmwareFlashService.Flash()`:

- записать success/error status и event с `request.PortName`;
- снять busy в `finally`-эквивалентном пути;
- независимо от результата перейти к существующему auto-discovery, если reader был закрыт или приложение ранее находилось в `WAITING`; иначе продолжить с reader другого порта;
- не заставлять discovery подключаться к вручную выбранному порту.

### Connected loop

Сохранить вызов firmware request processing перед очередным чтением telemetry. Если прошивался connection port, не обращаться к disposed reader: сразу перейти к `TryConnectSensor()` и получить новый reader/first message. Если прошивался другой порт, продолжить с существующим reader после завершения операции.

### Waiting loop

Внутри `TryConnectSensor()` обрабатывать firmware requests на каждой итерации до очередного telemetry probe. Для этого убрать предположение, что handler всегда получает `ref SerialSensorReader` с валидным объектом. После попытки прошивки продолжить auto-discovery.

Не запускать firmware operation параллельно с `ProcessApplicationUpdateRequests()` и shutdown. Существующий один orchestration worker сохраняет порядок и исключает одновременный app update/firmware flash.

Покрытие: AC-11–AC-23, AC-28–AC-31, AC-34.

## Этап 5. Безопасный запуск esptool и тестовый seam

В `FirmwareFlashService` перейти с общей строки `Arguments` на `ProcessStartInfo.ArgumentList`:

- `--chip`, `firmwareInfo.Chip`;
- `--port`, `request.PortName`;
- `--baud`, `460800`;
- `write-flash`, `0x0`, `firmwareInfo.AbsolutePath`.

Это сохраняет текущую команду и исключает ручное quoting порта/пути. Добавить минимальный process-runner seam или чистый builder `CreateStartInfo(...)`, чтобы unit-тест мог проверить аргументы без запуска `esptool.exe`.

Существующие правила поиска `Tools/esptool.exe`, сбора stdout/stderr и проверки exit code сохранить. В сообщение ошибки/успеха application layer добавляет фактический port.

Покрытие: AC-12, AC-18, AC-23, AC-30, AC-35.

## Этап 6. Тесты

### `BrightnessSensor.ConsoleApp.Tests`

Обновить `.csproj`, включив новые application/runtime типы, необходимые linked-source test project.

Добавить тесты:

- каталог сортирует, дедуплицирует, возвращает empty и safe error;
- auto-detected port становится default;
- manual port имеет приоритет и сохраняется после refresh;
- исчезнувший manual port возвращает выбор к auto либо null;
- программный auto-default не помечается manual;
- flash request содержит snapshot выбранного порта;
- второй request не ставится при busy/pending;
- порт, исчезнувший до выполнения, не запускает process;
- connected reader освобождается только при совпадении порта;
- request обрабатывается без reader в `WAITING`;
- после success/failure выполняется требуемое восстановление discovery;
- `ArgumentList` содержит точный flash contract.

Для orchestration-тестов выделить узкий класс/метод вместо запуска полного `BrightnessApplication.RunCore()`, чтобы подменять port catalog, firmware flasher и reader ownership без физического устройства.

UI-детали `ComboBox` проверить ручным smoke test, если текущая версия Terminal.Gui не предоставляет стабильный test harness. State transitions и refresh policy должны быть покрыты unit-тестами независимо от GUI.

## Этап 7. Документация

После реализации обновить:

- `README.md` — упомянуть auto-default и ручной выбор firmware port;
- `docs/build.md` — описать порт selector в packaged Update flow;
- `docs/getting-started.md` — добавить пользовательский сценарий прошивки;
- release notes следующей версии — отметить выбор порта;
- при изменении contributor validation — `CONTRIBUTING.md`.

Не изменять `docs/protocol.md`, если wire-контракт действительно остался прежним.

Покрытие: AC-28–AC-31 и пользовательская проверяемость фичи.

## Порядок реализации

1. Каталог портов и его unit-тесты.
2. Runtime state, selection policy и immutable request.
3. Тестируемый firmware operation handler и поддержка `WAITING`.
4. Безопасный `FirmwareFlashService` builder/runner seam.
5. `ComboBox`, локализация и layout.
6. Полный набор unit/regression tests.
7. Документация и ручная hardware-проверка.

Такой порядок сначала фиксирует поведение и ownership порта, а затем подключает UI к уже тестируемому контракту.

## Валидация

Из `pc-app/`:

```powershell
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```

Из корня для проверки packaged сценария:

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag dev
```

Затем выполнить ручной план из `ACCEPTANCE_CRITERIA.md` на Windows с ESP32-C6, как минимум одним дополнительным COM-устройством и bundled `Tools/esptool.exe`/`Firmware/*.bin`.

## Риски и меры

- **Порт исчез между выбором и flash.** Повторная проверка непосредственно перед process start и понятная ошибка.
- **UI refresh ошибочно становится manual override.** Флаг программного обновления и state-level distinction auto/manual.
- **Двойной клик запускает два процесса.** Single pending request плюс busy gating.
- **Reader удерживает flash-порт.** Явное сравнение портов и освобождение совпадающего reader до `esptool`.
- **Прошивка недоступна в `WAITING`.** Обработка той же очереди внутри discovery loop.
- **Статическая Windows API затрудняет тесты.** Инъецируемый provider для каталога портов и process-runner seam.
- **ComboBox ломает layout локализаций.** Проверка обычного/compact размеров на `en`, `ru`, `es`.

## Отклонённые варианты

- **Показывать выбор только после провала автообнаружения.** Не соответствует подтверждённому требованию постоянно доступного dropdown с auto-selected значением.
- **Сохранять порт в `appsettings.json`.** COM-имена нестабильны между компьютерами/переподключениями и ручной выбор относится только к flash-сеансу.
- **Использовать manual flash-port как runtime connection override.** Существенно меняет telemetry contract и выходит за scope.
- **Probe каждого порта при раскрытии.** Может блокировать UI и конфликтовать с открытым reader; friendly name получается из Windows PnP metadata без открытия порта.
- **Фоновый polling списка.** Не нужен: пользователь явно потребовал refresh при каждом раскрытии.

## Трассировка

| Требования | Основной владелец реализации | Критерии |
| --- | --- | --- |
| FR-1–FR-3 | `SerialPortCatalog`, `TerminalGuiDashboard` | AC-1–AC-5 |
| FR-4–FR-5 | `RuntimeStateStore`, `DashboardSnapshot` | AC-6–AC-10 |
| FR-6–FR-8 | firmware request model, orchestration, `FirmwareFlashService` | AC-11–AC-18 |
| FR-9 | state, UI gating, events/errors | AC-19–AC-23 |
| FR-10 | `Localizer`, Terminal.Gui layout | AC-24–AC-27 |
| Совместимость | discovery/config/flash regression tests | AC-28–AC-31 |
| Каталог портов | `SerialPortCatalog` tests | AC-32 |
| Selection state и request snapshot | `RuntimeStateStore` tests | AC-33 |
| Connected/WAITING orchestration | firmware operation handler tests | AC-34 |
| Аргументы `esptool.exe` | `FirmwareFlashService` builder tests | AC-35 |
| Build/test regression | `brightness-sensor.sln` | AC-36 |
