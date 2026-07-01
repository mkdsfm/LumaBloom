# Implementation Plan: User-Friendly Terminal UI Navigation

## Scope

Target: `pc-app/BrightnessSensor.ConsoleApp`.

The implementation modernizes only the Windows desktop terminal UI and related runtime settings persistence. It must preserve firmware behavior, serial telemetry format, device/profile identity, and monitor brightness integrations.

## Current Implementation Status

Implemented direction:

- `Terminal.Gui` interactive TUI.
- Plain redirected-output fallback.
- Top tabs: `Overview`, `Settings`, `Events`, `Diagnostics`.
- Settings right sidebar: `General`, `Calibration`, `Response/Реагирование`.
- Overview card layout.
- Auto/Manual brightness mode.
- Brightness curve editor with smooth interpolation.
- Processing settings editor.
- Live language switching and persistence.
- Windows autostart toggle.
- Reconnect/Waiting loop for missing sensor.
- Portable folder/zip build.
- Single-file exe build.

## Phase 1: Terminal.Gui Shell

Tasks:

1. Add `Terminal.Gui` dependency.
2. Build `ConsoleDashboardHost` around a `Terminal.Gui` application loop.
3. Keep telemetry and brightness processing on a background worker.
4. Keep redirected/non-interactive plain output fallback.
5. Reduce unnecessary refreshes:
   - refresh on state version change;
   - idle refresh for clock/status;
   - avoid full redraws while state is unchanged.

Status: implemented.

Validation:

- app starts in Windows Terminal;
- redirected mode prints plain status;
- UI remains responsive during telemetry updates.

## Phase 2: UI State Model

Tasks:

1. Add explicit state models:
   - `RuntimeScreen`
   - `SettingsSection`
   - `OverviewAction`
   - `BrightnessControlMode`
   - `UiLanguage`
   - update request records for language, processing, curve, test brightness, and autostart.
2. Extend `DashboardSnapshot` with:
   - active tab;
   - active Settings section;
   - processing values;
   - brightness curve;
   - autostart status.
3. Ensure telemetry refresh does not reset UI selection.

Status: implemented.

Validation:

- unit tests for state defaults and queued requests;
- active Settings section defaults to `General`.

## Phase 3: Navigation And Input

Tasks:

1. Top tabs outside the content frame.
2. Left/Right switch top-level tabs.
3. Up/Down move focus inside current tab.
4. Enter activates selected/focused control.
5. Esc closes modals/back.
6. Mouse activates tabs and visible controls.
7. Bottom hint line documents current input model.
8. Remove legacy letter hotkeys from primary UI.

Status: implemented.

Validation:

- manual keyboard navigation;
- mouse click navigation;
- no primary reliance on `q`, `p`, `c`, `l`.

## Phase 4: Overview

Tasks:

1. Build three-card Overview:
   - `Sensor`
   - `Ambient Light`
   - `Brightness Control`
2. Show:
   - `CONNECTED` / `WAITING`;
   - normalized light percent;
   - level text;
   - Auto/Manual mode;
   - manual target;
   - last applied;
   - monitor count.
3. Hide raw/diagnostic fields from Overview.
4. Add manual controls:
   - `Auto`
   - `Manual`
   - `-10`
   - `-1`
   - `+1`
   - `+10`
5. Remove the Overview-level `Apply` button.

Status: implemented.

Validation:

- Overview remains understandable before first telemetry;
- raw values only appear in Diagnostics.

## Phase 5: Brightness Control Mode

Tasks:

1. Add `BrightnessControlMode.Auto/Manual`.
2. Default to Auto.
3. Store runtime-only `ManualBrightnessPercent`.
4. Apply manual target to all enabled monitors.
5. On Manual -> Auto, force sensor-driven brightness to resume.

Status: implemented.

Validation:

- unit tests for clamping and manual application;
- manual smoke test against monitors;
- rapid Manual/Auto switching keeps `last applied` moving in Auto.

## Phase 6: Settings / General

Tasks:

1. Make `General` the first Settings sidebar item.
2. Open `General` by default.
3. Add language buttons:
   - English
   - Русский
   - Español
4. Persist language to `ui.language`.
5. Apply language immediately without restart.
6. Add Windows autostart:
   - read `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`;
   - show enabled/disabled state;
   - enable/disable through a visible button;
   - report registry errors without crashing.
7. Prevent overlap between language controls and autostart status.

Status: implemented.

Validation:

- change language, restart, verify persistence;
- toggle autostart and verify registry value;
- verify no stale glyphs under language buttons.

## Phase 7: Settings / Calibration

Tasks:

1. Replace the old visible recalibration wizard with a focused brightness-curve editor.
2. Add user-friendly explanation:
   - "Set how bright the display should be at different room light levels."
   - "Top row is room light, matching the value shown on the display."
   - "Bottom row is the monitor brightness LumaBloom should use."
   - "Values between points are blended smoothly."
3. Render a framed table:
   - row 1: ambient light points `0/25/50/75/100`;
   - row 2: display brightness buttons.
4. Center values by table cell.
5. Curve point modal:
   - input `0..100`;
   - `Test`;
   - `Confirm`;
   - `Cancel`.
6. Save confirmed points to `brightness.curve`.
7. If the curve is missing, create the full default curve on first save.
8. Smoothly interpolate brightness between neighboring user points.

Status: implemented.

Validation:

- edit each point;
- use Test without saving;
- restart and verify saved curve;
- verify interpolation tests pass.

## Phase 8: Settings / Response / Реагирование

Tasks:

1. Show effective processing settings.
2. Add modal editing for:
   - `adcMin`
   - `adcMax`
   - `invert`
   - `emaAlpha`
   - `hysteresisPercent`
   - `maxBrightnessStepPercent`
   - `gamma`
3. Validate values.
4. Persist to `appsettings.json`.
5. Rebuild runtime brightness processors after save.
6. Use radio-style true/false controls for `invert`.

Status: implemented.

Validation:

- unit tests for queued processing updates and config writer;
- manual test invalid/valid values;
- restart and verify persistence.

## Phase 9: Events And Diagnostics

Tasks:

1. Add dedicated Events tab.
2. Add dedicated Diagnostics tab.
3. Move raw/profiling/monitor details into Diagnostics.
4. Keep Diagnostics read-only.
5. Throttle repeated reconnect warnings.

Status: implemented.

Validation:

- Events empty/populated state;
- Diagnostics with raw telemetry and monitor details.

## Phase 10: Localization

Tasks:

1. Add `Localizer`.
2. Support:
   - English
   - Russian
   - Spanish
3. Resolve language from config and UI selection.
4. Persist UI selection.
5. Add localized labels, buttons, modal text, validation, hints.
6. Keep technical identifiers untranslated.
7. Ensure Russian labels fit in buttons/modals.

Status: implemented.

Validation:

- translation key tests;
- manual language switching;
- modal/button overlap checks.

## Phase 11: Waiting And Reconnect

Tasks:

1. Load effective settings before first telemetry.
2. Keep app open with no sensor connected.
3. Show `WAITING` with no telemetry.
4. On COM read error:
   - clear sensor snapshot;
   - close reader;
   - retry discovery/open/read;
   - resume when telemetry returns.
5. Detect stale telemetry and return to `WAITING`.
6. Use shorter reconnect probes to avoid UI stalls.
7. Avoid duplicate lifecycle updates and duplicate warnings.

Status: implemented.

Validation:

- start without sensor;
- disconnect/reconnect sensor;
- verify UI remains responsive.

## Phase 12: Layout And Resize

Tasks:

1. Remove fragile fixed ASCII dashboard.
2. Use Terminal.Gui frames/cards.
3. Put content one row below frame titles.
4. Keep top tabs outside main frame.
5. Keep footer outside main frame.
6. Give modal buttons stable widths.
7. Give language buttons stable widths.
8. Separate autostart status from language buttons.
9. Reduce redraw frequency.

Status: implemented.

Validation:

- resize terminal;
- verify no duplicated frames;
- verify no overlapping labels/buttons.

## Phase 13: Config Persistence

Tasks:

1. Add targeted `AppConfigWriter`.
2. Preserve unrelated config fields.
3. Persist:
   - language;
   - processing settings;
   - brightness curve.
4. Store autostart in Windows registry.
5. Use executable directory as default config location.
6. Auto-create minimal config when missing and no explicit path is provided.

Status: implemented.

Validation:

- config writer tests;
- restart after changing each setting;
- single-file exe starts without pre-existing config.

## Phase 14: Packaging

Tasks:

1. Keep existing portable folder/zip workflow:
   - `.codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py`
2. Add supported single-file publish command:

```powershell
dotnet publish pc-app/BrightnessSensor.ConsoleApp/BrightnessSensor.ConsoleApp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o pc-app/artifacts/single-file/win-x64 `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=None `
  /p:DebugSymbols=false
```

3. Ensure `appsettings.json` is not copied into publish output.
4. Ensure first run creates `appsettings.json` beside exe.

Status: implemented manually; optional future improvement is to add a dedicated script for single-file publishing.

Validation:

- `pc-app/artifacts/single-file/win-x64/` contains only `BrightnessSensor.ConsoleApp.exe` before first run;
- smoke-test single exe;
- confirm config creation.

## Phase 15: Documentation

Tasks:

1. Update feature spec.
2. Update acceptance criteria.
3. Update implementation plan.
4. Keep README/build-and-run aligned when user-facing behavior changes.
5. Mention:
   - Settings sections;
   - brightness curve;
   - Response modals;
   - autostart;
   - reconnect behavior;
   - portable and single-file artifacts.

Status: implemented in this update.

## Validation Commands

Run from `pc-app/`:

```powershell
dotnet build brightness-sensor.sln
dotnet test brightness-sensor.sln
```

Build portable zip from repo root:

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag dev-settings-tab-2026-07-01
```

Build single-file exe from repo root:

```powershell
dotnet publish pc-app/BrightnessSensor.ConsoleApp/BrightnessSensor.ConsoleApp.csproj -c Release -r win-x64 --self-contained true -o pc-app/artifacts/single-file/win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Remaining Follow-Up Options

1. Add a dedicated repo script for single-file publishing.
2. Add UI-level automated tests if a stable Terminal.Gui test harness is introduced.
3. Add event filtering if the event log becomes noisy.
4. Decide whether backend firmware recalibration actions should return to the UI later as a separate advanced flow.
