# Feature Spec: User-Friendly Terminal UI Navigation

## Summary

Modernize the Windows `pc-app` runtime UI into a user-friendly `Terminal.Gui` terminal application for LumaBloom.

The app must be usable without memorizing letter hotkeys: it has visible top tabs, a quiet green color theme, keyboard and mouse navigation, clear Overview/Settings/Events/Diagnostics screens, localized English/Russian/Spanish text, persistent settings, and diagnostics separated from the default user view.

This feature is scoped to `pc-app`. It must not change firmware behavior, serial telemetry format, device identity, profile defaults, or the ESP32 calibration protocol.

## Current Direction

- Interactive UI library: `Terminal.Gui`.
- Redirected/non-interactive fallback: plain status output; legacy Spectre renderer may remain only as a fallback/render-test path.
- Platform: Windows.
- Primary user: a normal desktop user, not a firmware maintainer.
- Theme: restrained green LumaBloom colors, with active controls highlighted.
- Release artifacts:
  - portable folder/zip;
  - optional single-file exe build where .NET/runtime dependencies are bundled into one `.exe`.

## Top-Level Navigation

Visible top-level tabs:

- `Overview`
- `Settings`
- `Events`
- `Diagnostics`

Navigation rules:

1. Left/Right switch top-level tabs.
2. Up/Down move focus within the active tab.
3. Enter activates the focused control.
4. Esc closes the active modal or backs out of the current nested flow.
5. Mouse clicks activate visible tabs and controls.
6. Ctrl+C requests shutdown.
7. Legacy letter hotkeys such as `q`, `p`, `c`, and `l` are not primary controls.
8. Bottom help text is shown outside the main content frame.

## Overview

Purpose: answer the user question: "Is the sensor connected, how much light is there, and what is brightness control doing?"

Overview contains exactly three primary cards:

- `Sensor`
  - `CONNECTED` / `WAITING`
  - last read time
- `Ambient Light`
  - normalized value `0..100%`
  - text level: `DARK`, `LOW`, `MID`, `BRIGHT`, `MAX`
- `Brightness Control`
  - mode: `Auto` / `Manual`
  - manual target percent
  - last applied brightness
  - active monitor count
  - controls: `Auto`, `Manual`, `-10`, `-1`, `+1`, `+10`

Overview must not show:

- raw/ADC values;
- estimated lux;
- `deviceId`, `sensorId`, `profileId`;
- COM port or baud details;
- profile summaries;
- monitor source/name/error internals;
- processing internals such as normalized/filtered values.

Those details belong in `Diagnostics`.

## Brightness Control

Runtime mode:

- `Auto`
  - default startup mode;
  - computes brightness from the current sensor value and effective settings;
  - uses the user brightness curve with smooth interpolation;
  - existing EMA, hysteresis, max step, and gamma processing still apply.
- `Manual`
  - uses one percent value `0..100`;
  - applies the same target to all brightness-capable monitors;
  - does not persist as an app setting;
  - switching back to `Auto` immediately resumes sensor-driven brightness.

## Settings

The top-level tab is `Settings`. It contains a right sidebar.

Sidebar order and default section:

1. `General`
2. `Calibration`
3. `Response` / `Реагирование`

`General` opens by default.

### Settings / General

Purpose: user preferences.

Controls:

- language selection:
  - `English`
  - `Русский`
  - `Español`
- Windows autostart:
  - status: enabled/disabled;
  - action button: enable/disable autostart.

Language requirements:

- changes apply immediately;
- selected language persists to active `appsettings.json` as `ui.language`;
- valid values are `auto`, `en`, `ru`, `es`;
- save errors are reported as user-level events without crashing.

Autostart requirements:

- implemented through current-user Windows startup registry:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`;
- no administrator rights required;
- state is read on startup;
- toggling applies immediately;
- errors are reported as user-level events without crashing.

### Settings / Calibration

Purpose: configure the user brightness curve.

The section intentionally focuses on the practical user goal:

> When ambient light is X%, display brightness should be Y%.

Current explanatory text:

```text
Настройте, какую яркость экрана LumaBloom будет выбирать
при разном уровне освещения.

Верхняя строка — освещенность в комнате (значение на дисплее).
Нижняя строка — желаемая яркость дисплея.
Нажмите на значение яркости, чтобы изменить его.
Между точками яркость меняется плавно.
```

UI requirements:

- show a framed two-row table;
- top row: ambient light points `0%`, `25%`, `50%`, `75%`, `100%`;
- bottom row: editable display brightness values;
- values are centered by table cell;
- selecting a brightness value opens a modal;
- modal accepts `0..100`;
- modal actions:
  - `Test`: immediately applies the value to monitors for preview;
  - `Confirm`: persists the curve point;
  - `Cancel`: closes without saving;
- confirmed values persist to `brightness.curve` in active `appsettings.json`;
- if no curve exists yet, saving the first point creates a full default `0/25/50/75/100` curve;
- Auto mode interpolates smoothly between neighboring points instead of jumping by thresholds.

The previous visible recalibration wizard actions are not part of the current Calibration screen. Firmware/device calibration behavior may remain in backend code, but this UI section is now centered on the brightness curve editor.

### Settings / Response / Реагирование

Purpose: tune signal response without editing JSON.

The section shows effective `processing` values and opens a modal editor per parameter:

- `adcMin`: integer, must be less than `adcMax`;
- `adcMax`: integer, must be greater than `adcMin`;
- `invert`: radio choice `true` / `false`;
- `emaAlpha`: decimal in `(0, 1]`;
- `hysteresisPercent`: integer in `0..100`;
- `maxBrightnessStepPercent`: integer in `1..100`;
- `gamma`: decimal greater than `0`.

On Confirm:

- value persists to active `appsettings.json`;
- runtime brightness processors are rebuilt;
- new value applies without restart.

If save/apply fails:

- previous runtime settings remain active;
- an error event is shown.

## Events

Purpose: dedicated runtime event log.

Must show:

- timestamp;
- severity;
- message;
- empty state when no events exist.

Repeated reconnect/discovery warnings should be throttled so the UI does not flicker or fill the event log with duplicate messages.

## Diagnostics

Purpose: advanced troubleshooting.

Must show:

- raw telemetry:
  - `deviceId`
  - `sensorId`
  - `value`
  - `raw`
  - `calibrated`
  - device timestamp
  - received timestamp
- profile/settings summary;
- measurement kind;
- generic profile flag;
- COM port and baud;
- monitor source/name/status/error;
- requested/applied brightness;
- normalized/filtered signal values;
- calibration status/details.

Diagnostics is read-only.

## Connection And Waiting Behavior

The UI must survive missing or disconnected hardware.

Requirements:

1. Settings load immediately from `appsettings.json` or a generated default config, even before first telemetry.
2. If no sensor is connected, app stays open and shows `WAITING`.
3. If a COM read error occurs, app:
   - clears the latest sensor;
   - sets status to `WAITING`;
   - closes the current reader;
   - periodically retries discovery/open/read;
   - returns to `RUNNING` when telemetry returns.
4. If COM stays open but telemetry becomes stale, UI returns to `WAITING`.
5. Reconnect attempts should use short probes and avoid UI stalls.
6. Telemetry refresh/reconnect must not reset the active tab, Settings section, modal state, language, or curve values.

## Persistence

User-editable Settings must persist and be used after restart:

- `ui.language`;
- `processing.*`;
- `brightness.curve`;
- Windows autostart registry state.

Manual brightness mode remains runtime-only.

Default config behavior:

- default config path is next to the executable: `AppContext.BaseDirectory/appsettings.json`;
- if no config exists and no explicit config path is provided, the app creates a minimal valid config;
- this enables a single-file exe to start cleanly after download/copy.

## Localization

Supported languages:

- English (`en`)
- Russian (`ru`)
- Spanish (`es`)

Requirements:

1. User-facing labels, actions, modal descriptions, validation errors, empty states, and bottom hints are localizable.
2. Language changes apply immediately.
3. Layout must account for Russian and Spanish text lengths.
4. Technical identifiers remain stable where useful:
   - `deviceId`
   - `sensorId`
   - `raw`
   - `calibrated`
   - `profileId`
   - COM names
5. Missing translations should fail tests or fall back explicitly to English.

## Layout And Resize

Requirements:

- no duplicated frames after resize;
- no overlapping text in normal desktop terminal sizes;
- content inside frames starts one row below the top border/title;
- modal buttons must fit Russian labels;
- language and autostart controls must not overlap;
- compact mode should prefer readable primary content over showing every detail;
- UI refresh should avoid unnecessary full redraws and flicker.

## Packaging

Supported artifacts:

1. Portable folder:
   - `pc-app/artifacts/portable/win-x64/`
2. Portable zip:
   - `pc-app/artifacts/portable/luma-bloom-pc-app_<tag>_win-x64-portable.zip`
3. Single-file exe:
   - `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`

Single-file requirements:

- .NET runtime and managed/native dependencies are bundled into one exe;
- no pdb/dll sidecars are required for distribution;
- on first launch, the app creates `appsettings.json` next to the exe for persisted settings;
- autostart points at the actual executable path.

## Non-Goals

- No firmware changes.
- No serial protocol changes.
- No device profile identity changes.
- No built-in hardware profile default changes.
- No replacement of Windows monitor brightness integrations.

## Validation

Required commands from `pc-app/`:

```powershell
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```

Manual validation should include:

- start with sensor disconnected;
- connect/reconnect sensor;
- switch Auto/Manual;
- edit brightness curve and use Test;
- edit all Response parameters including `invert` radio buttons;
- switch languages;
- toggle autostart;
- open Events and Diagnostics;
- resize terminal;
- run portable exe;
- run single-file exe without existing `appsettings.json`.
