---
name: esp32-release-flash
description: Build merged release binaries for every firmware project and hardware variant in the LumaBloom repository, and optionally flash one explicitly selected ESP32 artifact. Use for firmware release builds, merged .bin generation, or USB flashing.
---

# Esp32 Release Flash

## Overview

Build every release-ready firmware artifact through the scripts owned by the firmware projects. When requested, flash one explicitly selected merged binary.

## Workflow

1. Resolve the artifact version from the same MinVer configuration as `pc-app`. Pass `--tag` only when the user explicitly requests a version override.
2. Run `scripts/build_release_and_flash.py` from the repository root. It discovers every firmware project and invokes its required `build_merged.py` with the resolved version.
3. Before each project build, remove old binaries and manifests matching the resolved version. Treat a firmware directory without `build_merged.py`, or an artifact without its manifest, as an error.
4. If the user asks to flash and multiple artifacts exist, require the exact `--firmware-file`. Never choose a hardware variant implicitly.
5. Use `--skip-build` only when the user explicitly wants to reuse valid existing variant build artifacts.
6. Report:
   - the generated binary path
   - the exact filename
   - whether flashing was performed
   - any follow-up action the user should take on the device

## Commands

### Build a readable merged binary

```powershell
python .codex-skill-staging/esp32-release-flash/scripts/build_release_and_flash.py --repo-root .
```

### Create a readable binary from existing build artifacts

```powershell
python .codex-skill-staging/esp32-release-flash/scripts/build_release_and_flash.py --repo-root . --skip-build
```

### Build and flash in one step

```powershell
python .codex-skill-staging/esp32-release-flash/scripts/build_release_and_flash.py --repo-root . --tag 2.1.0 --flash-port COM8 --firmware-file luma_bloom_esp32c6-display_2.1.0_merged.bin
```

## Naming Rules

- Each firmware project owns its artifact names.
- Unless overridden, the tag is `pc-app`'s MinVer version with build metadata after `+` removed, matching `AppVersion.Current`.
- Every artifact must include the requested tag and end in `_merged.bin`.
- Every artifact must have a `<binary>.manifest.json` sidecar.
- Each project writes artifacts to its own `build/release/` directory.

See `references/output-conventions.md` for the exact naming and output rules.

## Notes

- Firmware-specific build commands belong in each project's `build_merged.py`, not in this skill. The project script may use ESP-IDF, Arduino, PlatformIO, STM32 tooling, or any other required toolchain.
- The current ESP32-C6 project builds both `esp32c6-display` and `esp32c6-touch` by default.
- When flashing, it uses `esptool` against the merged binary at `0x0`.
