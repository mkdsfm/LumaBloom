---
name: luma-bloom-release
description: Coordinate the full LumaBloom release workflow for this repository. Use when Codex needs to produce the firmware release payload for bundling, the versioned Windows portable zip, and the English release notes and the request specifies both a source tag and a target tag.
---

# LumaBloom Release

Use this skill as the top-level release coordinator. It should not reimplement the detailed steps that already belong to lower-level skills.

## Inputs

Require:

- source tag, for example `0.2.1`
- target tag, for example `0.3.0`

Optional:

- whether to build artifacts or only draft notes
- whether to flash the firmware after building

## Skill Routing

Use these skills instead of duplicating their workflows:

1. `.codex-skill-staging/esp32-release-flash`
   Responsibility:
   build the firmware release payload in `firmware/firmware_esp32c6/build/release/`
2. `.codex-skill-staging/pc-app-portable-release`
   Responsibility:
   build `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip` with `Tools/esptool.exe` and, when available, a bundled ESP32-C6 firmware release folder for the in-app Update flow
3. `.codex-skill-staging/release-notes-github`
   Responsibility:
   write the English GitHub release description for `<from-tag> -> <to-tag>`

## Workflow

1. Confirm the tag range.
2. If the user wants release artifacts, build the firmware binary through `esp32-release-flash`.
3. Only after the firmware step succeeds, build the Windows portable zip through `pc-app-portable-release`.
4. Verify that the portable package contains:
   - `BrightnessSensor.ConsoleApp.exe`
   - `Tools/esptool.exe`
   - the matching firmware file for the target tag inside `Firmware/`
5. Draft the release notes through `release-notes-github`.
6. Report:
   - the final artifact filenames
   - the final artifact paths
   - the completed release note text

## Output Contract

When artifacts are requested, the release should end with these filenames:

- `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`

The Windows portable artifact should contain:

- `BrightnessSensor.ConsoleApp.exe`
- `Tools/esptool.exe`
- the bundled firmware release folder in `Firmware/` when the firmware payload was built first
- the firmware file in that folder must match the target tag for release builds

When notes are requested, the text should follow the English structure defined by `release-notes-github`.

## Resources

- See `references/subskills.md` for the division of responsibilities.
