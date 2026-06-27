# Codex Skills for Users

This repo can be operated through Codex skills when you want repeatable build and flashing workflows without remembering every command.

## Recommended Skills

- `esp32-release-flash`
  Use this when you want Codex to build a readable merged ESP32 firmware binary and optionally flash it to the board.

## Typical Requests

You can ask Codex things like:

- `use the esp32-release-flash skill to build a release file for esp32c6`
- `use the esp32-release-flash skill to find the COM port and flash esp32c6`
- `build a merged bin for firmware_esp32c6 and name it brightness_sensor_esp32c6_calibrated`

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
- For this repo, a readable release name such as `brightness_sensor_esp32c6_calibrated.bin` is recommended when the firmware behavior has changed.
