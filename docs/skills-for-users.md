# Codex Skills for Users

This repo can be operated through Codex skills when you want repeatable build and flashing workflows without remembering every command.

## Recommended Skills

- `esp32-release-flash`
  Use this when you want Codex to build a readable merged ESP32 firmware binary and optionally flash it to the board.
- `pc-app-portable-release`
  Use this when you want Codex to build the versioned Windows portable zip for the desktop application.
- `release-notes-github`
  Use this when you want Codex to draft or update GitHub release notes for this repo in English, and you can specify both the source tag and the target tag.
- `luma-bloom-release`
  Use this when you want Codex to coordinate the full release flow across firmware, Windows zip, and release notes, with an explicit source tag and target tag.

## Typical Requests

You can ask Codex things like:

- `use the esp32-release-flash skill to build a release file for esp32c6`
- `use the esp32-release-flash skill to find the COM port and flash esp32c6`
- `build a merged bin for firmware_esp32c6 and name it luma_bloom_esp32c6_calibrated`
- `use the pc-app-portable-release skill to build the Windows zip for tag 0.3.0`
- `use the release-notes-github skill and write release notes; source tag: 0.2.1, target tag: 0.3.0`
- `use the luma-bloom-release skill; source tag: 0.2.1, target tag: 0.3.0; create release artifacts and write release notes`
- `use the luma-bloom-release skill; source tag: 0.2.1, target tag: 0.3.0; prepare the full release`

## What `esp32-release-flash` Does

For `firmware/firmware_esp32c6/` the skill can:

- detect that the project already has `build/flasher_args.json` and `build/config.env`;
- rebuild the app and bootloader when needed;
- create a merged binary in `build/release/`;
- flash the merged binary to a chosen `COM` port;
- reuse existing artifacts with `--skip-build` when only reflashing is needed.

## User-Facing Workflow

1. Connect the ESP32 board over USB.
2. Ask Codex to use `esp32-release-flash`.
3. If you do not know the port, say so explicitly; Codex can probe available `COM` ports.
4. Wait for Codex to report:
   - the generated binary path
   - the exact file name
   - whether flashing succeeded
5. After flashing, reopen the device or start `pc-app` if the board was reset.

## Recommended Wording

- For build only: `use the esp32-release-flash skill to create a release bin for esp32c6`
- For build and flash: `use the esp32-release-flash skill to create the firmware file and update esp32c6`
- For reflashing an existing build: `use the esp32-release-flash skill to reflash the existing esp32c6 build without rebuilding`

## Notes

- The skill is best suited for `ESP-IDF` projects like `firmware_esp32c6`.
- Flashing may fail if the `COM` port is busy; in that case close `pc-app`, serial monitors, Arduino, PlatformIO, or any terminal attached to the port.
- For this repo, a readable release name such as `luma_bloom_esp32c6_calibrated.bin` is recommended when the firmware behavior has changed.
- The repo-local release-note skill lives in `.codex-skill-staging/release-notes-github/`.
- The repo-local Windows zip skill lives in `.codex-skill-staging/pc-app-portable-release/`.
- The repo-local orchestration skill lives in `.codex-skill-staging/luma-bloom-release/`.
- For these release skills, explicitly specify both the source tag and the target tag. Example: `source tag: 0.2.1, target tag: 0.3.0`.
- For release artifacts produced by this skill, include the target version in both filenames, for example `luma_bloom_esp32c6_0.3.0_merged.bin` and `luma-bloom-pc-app_0.3.0_win-x64-portable.zip`.
