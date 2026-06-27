---
name: esp32-release-flash
description: Build readable ESP32/ESP-IDF release binaries and optionally flash them to a connected device. Use when the user asks to create a merged firmware binary with a human-readable name, prepare a release artifact, regenerate a flashable .bin from an ESP-IDF project, or flash that binary to an ESP32 board over USB serial.
---

# Esp32 Release Flash

## Overview

Build release-ready merged ESP32 firmware binaries with readable filenames and, when requested, flash them to a device. Prefer the bundled script for deterministic builds and flashing on Windows ESP-IDF setups.

## Workflow

1. Confirm the target project is an ESP-IDF firmware project with a `build/flasher_args.json` or `build/config.env`.
2. Run `scripts/build_release_and_flash.py` with the project path and a readable release name.
   - If the project currently does not rebuild cleanly but valid artifacts already exist in `build/`, use `--skip-build`.
3. If the user asks to flash, pass `--flash-port COMx` so the same script writes the merged binary to offset `0x0`.
4. Report:
   - the generated binary path
   - the exact filename
   - whether flashing was performed
   - any follow-up action the user should take on the device

## Commands

### Build a readable merged binary

```powershell
python scripts/build_release_and_flash.py --project C:\path\to\firmware --release-name my-readable-name
```

### Create a readable binary from existing build artifacts

```powershell
python scripts/build_release_and_flash.py --project C:\path\to\firmware --release-name my-readable-name --skip-build
```

### Build and flash in one step

```powershell
python scripts/build_release_and_flash.py --project C:\path\to\firmware --release-name my-readable-name --flash-port COM8
```

## Naming Rules

- Prefer user-provided names when available.
- Normalize names to lowercase with `a-z`, `0-9`, `_` and `-`.
- Append `.bin` automatically if the user omits it.
- Write release artifacts to `build/release/`.

See `references/output-conventions.md` for the exact naming and output rules.

## Notes

- The script is Windows-first and tuned for local ESP-IDF installs similar to `C:\Espressif\tools` and `C:\esp\...`.
- The script rebuilds the app, ensures `bootloader.bin` exists, then merges `bootloader`, `partition-table`, and `app` into one flashable binary.
- When flashing, it uses `esptool` against the merged binary at `0x0`.
