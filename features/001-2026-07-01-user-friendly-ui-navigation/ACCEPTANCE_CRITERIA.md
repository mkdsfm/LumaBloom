# Acceptance Criteria: User-Friendly Terminal UI Navigation

## Build And Test

1. `dotnet build brightness-sensor.sln` succeeds from `pc-app/`.
2. `dotnet test brightness-sensor.sln` succeeds from `pc-app/`.
3. Release publish does not require a machine-wide .NET runtime.
4. No new warnings are allowed unless explicitly documented.

## Top-Level Navigation

1. The visible tabs are:
   - `Overview`
   - `Settings`
   - `Events`
   - `Diagnostics`
2. Left/Right switch top-level tabs.
3. Up/Down move focus within the active tab.
4. Enter activates the focused control.
5. Esc closes modals or backs out of nested UI.
6. Mouse clicks activate visible tabs and controls.
7. Ctrl+C requests shutdown.
8. Legacy letter hotkeys `q`, `p`, `c`, and `l` are not primary UI controls.
9. Active tabs and active controls are highlighted with restrained green LumaBloom styling.
10. Bottom navigation hints are outside the main content frame.

## Overview

1. `Overview` is the default top-level tab.
2. It has exactly three user-facing cards:
   - `Sensor`
   - `Ambient Light`
   - `Brightness Control`
3. `Sensor` shows `CONNECTED` or `WAITING`.
4. `Ambient Light` shows normalized `0..100%`.
5. `Ambient Light` shows one of:
   - `DARK`
   - `LOW`
   - `MID`
   - `BRIGHT`
   - `MAX`
6. `Brightness Control` shows:
   - `Auto` / `Manual`;
   - manual target;
   - last applied brightness;
   - active monitor count.
7. Manual controls are visible:
   - `Auto`
   - `Manual`
   - `-10`
   - `-1`
   - `+1`
   - `+10`
8. Manual brightness applies one value to all enabled brightness-capable monitors.
9. Switching Manual -> Auto immediately resumes sensor-driven brightness.
10. `Overview` does not show raw telemetry, ADC, lux, COM details, profile ids, or monitor internals.
11. `Overview` does not show recalibration actions.

## Settings

1. The top-level tab is named `Settings`.
2. The Settings right sidebar order is:
   - `General`
   - `Calibration`
   - `Response` / `Реагирование`
3. `General` opens by default when entering Settings.
4. The active sidebar item is visually distinct.
5. Top-level Left/Right tab navigation still works while Settings is open.
6. Settings state is not reset by telemetry refresh.

## Settings / General

1. Language choices are visible:
   - `English`
   - `Русский`
   - `Español`
2. Language changes apply immediately.
3. Language persists to active `appsettings.json` as `ui.language`.
4. The selected language survives app restart.
5. General shows Windows autostart status.
6. General has a button to enable/disable Windows autostart.
7. Autostart uses current-user Windows startup registry and does not require admin rights.
8. Autostart state is read on startup.
9. Language controls and autostart text/buttons do not overlap or leave stale glyphs.
10. Save/apply errors are reported as user-visible events without crashing.

## Settings / Calibration

1. Calibration section describes the brightness curve in user-friendly text.
2. It shows a framed table with two rows:
   - ambient light points;
   - display brightness values.
3. Ambient light points are:
   - `0%`
   - `25%`
   - `50%`
   - `75%`
   - `100%`
4. Values are centered within the table cells.
5. The display-brightness values are clickable/focusable controls.
6. Selecting a display-brightness value opens a modal.
7. The modal validates brightness input as `0..100`.
8. The modal provides:
   - `Test`
   - `Confirm`
   - `Cancel`
9. `Test` immediately applies the entered brightness for preview and does not persist the point.
10. `Confirm` persists the point to `brightness.curve`.
11. `Cancel` closes without saving.
12. If `brightness.curve` does not exist, saving a point creates a full `0/25/50/75/100` curve.
13. Auto brightness smoothly interpolates between curve points.
14. Saved curve points are used after restart.
15. The old visible current-brightness/manual-target recalibration wizard controls are not shown on this screen.

## Settings / Response / Реагирование

1. The section shows effective processing values.
2. Each parameter opens a modal editor.
3. Validation rules:
   - `adcMin < adcMax`;
   - `adcMax > adcMin`;
   - `emaAlpha` in `(0, 1]`;
   - `hysteresisPercent` in `0..100`;
   - `maxBrightnessStepPercent` in `1..100`;
   - `gamma > 0`.
4. `invert` uses radio-style choices `true` and `false`, not free-form text input.
5. Confirm persists the value to active `appsettings.json`.
6. Confirm applies the value without app restart.
7. If save/apply fails, previous runtime settings remain active.
8. Saved processing values are used after restart.

## Events

1. `Events` is a dedicated tab.
2. It shows timestamp, severity, and message.
3. It has an empty state.
4. Repeated reconnect/discovery warnings are throttled.
5. Events do not replace Diagnostics.

## Diagnostics

1. `Diagnostics` is a dedicated tab.
2. It shows raw telemetry:
   - `deviceId`
   - `sensorId`
   - `value`
   - `raw`
   - `calibrated`
   - timestamps
3. It shows profile and connection details:
   - profile/settings summary;
   - measurement kind;
   - generic profile flag;
   - COM port and baud.
4. It shows monitor diagnostics:
   - source/name;
   - status/error;
   - requested/applied brightness;
   - normalized/filtered signal values.
5. It remains read-only.

## Localization

1. UI supports English, Russian, and Spanish.
2. `auto`, `en`, `ru`, and `es` are valid config values.
3. Unsupported configured language fails validation clearly.
4. Missing translation keys are covered by tests or fall back explicitly.
5. Layout remains usable in all three languages.
6. Russian modal buttons do not overlap.

## Waiting And Reconnect

1. App starts with no sensor connected and does not exit.
2. App shows `WAITING` when no valid telemetry is available.
3. Settings load before first telemetry.
4. If COM read fails, app returns to `WAITING` and retries discovery.
5. If telemetry becomes stale, app returns to `WAITING`.
6. When telemetry returns, app reconnects and resumes normal processing.
7. Reconnect does not reset selected tab, Settings section, modal state, language, processing settings, or curve values.
8. Reconnect attempts do not cause visible periodic UI freezes.

## Layout

1. Text inside frames starts one row below the frame title/top border.
2. Resize does not duplicate frames.
3. Resize does not leave broken old text artifacts.
4. Normal desktop terminal sizes show no overlapping controls.
5. Compact mode remains readable.
6. UI refresh avoids unnecessary redraws and flicker.

## Packaging

1. Portable folder publish exists:
   - `pc-app/artifacts/portable/win-x64/`
2. Portable zip exists:
   - `pc-app/artifacts/portable/luma-bloom-pc-app_<tag>_win-x64-portable.zip`
3. Single-file exe publish exists:
   - `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`
4. Single-file folder can contain only the exe before first run.
5. Single-file exe starts without a pre-existing `appsettings.json`.
6. First single-file run creates `appsettings.json` next to the exe.
7. Settings changed from the single-file exe persist to that generated config.
8. Autostart points to the actual exe path.

## Manual Test Plan

1. Start the app with no sensor connected.
2. Confirm the app stays open and shows `WAITING`.
3. Connect the sensor and confirm it reaches `CONNECTED`.
4. Disconnect/reconnect the sensor and confirm the app recovers.
5. Navigate all tabs with keyboard and mouse.
6. Switch Manual/Auto and adjust brightness.
7. Edit every curve point and use `Test`.
8. Restart and confirm curve values persist.
9. Change language to Russian and Spanish.
10. Restart and confirm language persists.
11. Toggle autostart on/off.
12. Edit every Response parameter, including `invert` radio.
13. Confirm invalid Response inputs stay in modal with an error.
14. Open Events and Diagnostics.
15. Resize terminal.
16. Run portable folder exe.
17. Run single-file exe from an empty folder.
18. Press Ctrl+C and confirm clean shutdown.

## Validation Commands

Run from `pc-app/`:

```powershell
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```
