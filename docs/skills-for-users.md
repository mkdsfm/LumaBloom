# Codex Skills for Users

This repo can be operated through Codex skills when you want repeatable build and flashing workflows without remembering every command.

## Recommended Skills

- `esp32-release-flash`
  Use this when you want Codex to run every firmware project's own merged-binary script and optionally flash one explicitly selected ESP artifact.
- `pc-app-portable-release`
  Use this when you want Codex to build every firmware project and the versioned Windows portable zip, including all matching merged binaries, their manifests, and `Tools/esptool.exe` for the in-app Update flow.
- `release-notes-github`
  Use this when you want Codex to draft or update GitHub release notes for this repo in English, and you can specify both the source tag and the target tag.
- `luma-bloom-release`
  Use this when you want Codex to coordinate the full release flow across firmware, Windows zip, and release notes, with an explicit source tag and target tag.

## Typical Requests

You can ask Codex things like:

- `use the esp32-release-flash skill to build all firmware files for tag 2.1.0`
- `use the esp32-release-flash skill to flash luma_bloom_esp32c6-touch_2.1.0_merged.bin to COM8`
- `use the esp32-release-flash skill to flash luma_bloom_esp32c3-supermini_2.1.0_merged.bin to COM8`
- `use the pc-app-portable-release skill to build the Windows zip for tag 0.3.0`
- `use the pc-app-portable-release skill to build the Windows zip for tag 0.3.0 with the firmware release folder bundled for the Update tab`
- `use the release-notes-github skill and write release notes; source tag: 0.2.1, target tag: 0.3.0`
- `use the luma-bloom-release skill; source tag: 0.2.1, target tag: 0.3.0; create release artifacts and write release notes`
- `use the luma-bloom-release skill; source tag: 0.2.1, target tag: 0.3.0; prepare the full release`

## What `esp32-release-flash` Does

The build skill:

- discovers every immediate project under `firmware/`;
- requires each project to provide its own `build_merged.py` adapter for its actual toolchain;
- builds every variant exposed by every project into that project's `build/release/` directory;
- currently creates both `esp32c6-display` and `esp32c6-touch` binaries for the ESP32-C6 project;
- creates the `esp32c3-supermini` binary for the displayless ESP32-C3 project;
- flashes one explicitly selected merged ESP binary to a chosen `COM` port;
- reuse existing artifacts with `--skip-build` when only reflashing is needed.

## User-Facing Workflow

1. Connect the ESP32 board over USB.
2. Ask Codex to use `esp32-release-flash`.
3. If you do not know the port, say so explicitly; Codex can probe available `COM` ports.
4. Wait for Codex to report:
   - all generated binary paths and filenames
   - whether flashing succeeded
5. After flashing, reopen the device or start `pc-app` if the board was reset.

## Recommended Wording

- For build only: `use the esp32-release-flash skill to create all firmware release bins for tag 2.1.0`
- For build and flash: `use the esp32-release-flash skill to build all firmware and flash <exact-file> to COM8`
- For reflashing an existing build: `use the esp32-release-flash skill to reflash the existing esp32c6 build without rebuilding`

## Notes

- Build logic is project-local and is not limited to ESP-IDF. New non-ESP firmware must provide the same `build_merged.py` entrypoint while implementing its own toolchain commands internally.
- When no tag is supplied, build skills use the MinVer version resolved for `pc-app`, normalized the same way as `AppVersion.Current`.
- Flashing may fail if the `COM` port is busy; in that case close `pc-app`, serial monitors, Arduino, PlatformIO, or any terminal attached to the port.
- The repo-local release-note skill lives in `.codex-skill-staging/release-notes-github/`.
- The repo-local Windows zip skill lives in `.codex-skill-staging/pc-app-portable-release/`.
- The repo-local orchestration skill lives in `.codex-skill-staging/luma-bloom-release/`.
- For these release skills, explicitly specify both the source tag and the target tag. Example: `source tag: 0.2.1, target tag: 0.3.0`.
- For the Windows portable artifact produced by these skills, include the target version in the zip filename, for example `luma-bloom-pc-app_0.3.0_win-x64-portable.zip`.
- For a reliable bundled firmware flasher, keep the official standalone Windows `esptool.exe` in `third_party/esptool/win-x64/esptool.exe` before building the portable zip.
- The portable `pc-app` release carries `BrightnessSensor.ConsoleApp.exe`, `Tools/esptool.exe`, and all target-tagged artifacts with their `<binary>.manifest.json` sidecars from `firmware/<project>/build/release/` inside `Firmware/` for the Update screen.
