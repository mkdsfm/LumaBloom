# Contributing

Thanks for improving LumaBloom. Keep changes focused and validate the track you touch.

## Project Focus

The documented hardware targets are Waveshare `ESP32-C6-LCD-1.47` with KY-018 and the displayless ESP32-C3 Super Mini + KY-018 build. Both use the same printable flower above the pot; the checked-in lower pot fits ESP32-C6, while the different ESP32-C3 pot is not yet available.

The PC app is Windows-only by design.

## Before Editing

- Read [`README.md`](README.md) for project context.
- Read [`docs/protocol.md`](docs/protocol.md) before changing telemetry or calibration.
- Read [`docs/device-profiles.md`](docs/device-profiles.md) before changing profile resolution or defaults.
- Read [`hardware/README.md`](hardware/README.md) before changing wiring, BOM, enclosure, or assembly docs.

## Validation

Choose the smallest validation that meaningfully covers the change:

- PC app logic: run `dotnet build brightness-sensor.sln` and preferably `dotnet test brightness-sensor.sln` from `pc-app/`.
- Parser, config, or profile changes: prioritize `BrightnessSensor.ConsoleApp.Tests` and `BrightnessSensor.DeviceReading.Tests`.
- ESP32-C6 firmware changes: build `firmware/firmware_esp32c6/` with target `esp32c6` and validate both display variants when applicable.
- ESP32-C3 firmware changes: build `firmware/firmware_esp32c3_supermini/` with target `esp32c3`.
- Hardware flashing should be called out when not performed.
- Protocol changes: validate both producer and consumer, or explicitly document what was not validated.
- Docs-only changes: no build is required, but links and command examples should match the repo layout.

## Documentation Rules

Update docs when behavior changes in any user-visible way, especially:

- telemetry format;
- calibration flow;
- example config fields;
- profile selection behavior;
- build, flash, or release artifact instructions;
- hardware wiring, BOM, assembly, or printable enclosure assets.

## Local Files

- Treat `pc-app/appsettings.json` as a local machine file.
- Avoid editing generated outputs under `build/`, `bin/`, `obj/`, or ESP-IDF generated config files unless the task explicitly requires it.
