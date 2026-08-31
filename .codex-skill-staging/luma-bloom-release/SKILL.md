---
name: luma-bloom-release
description: Coordinate the full LumaBloom release workflow by building every firmware project through its own script, packaging all matching merged binaries into the versioned Windows portable zip, and preparing English release notes. Use when both source and target tags are provided.
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
   discover every firmware project and run its own `build_merged.py`; optionally flash one explicitly selected ESP artifact
2. `.codex-skill-staging/pc-app-portable-release`
   Responsibility:
   build `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip` with `Tools/esptool.exe` and every matching firmware artifact in `Firmware/`
3. `.codex-skill-staging/release-notes-github`
   Responsibility:
   write the English GitHub release description for `<from-tag> -> <to-tag>`

## Workflow

1. Confirm the tag range.
2. If the user wants release artifacts, build every firmware project through `esp32-release-flash`.
3. Only after every firmware script succeeds, build the Windows portable zip through `pc-app-portable-release --skip-firmware-build` to avoid rebuilding the same artifacts.
4. Verify that the portable package contains:
   - `BrightnessSensor.ConsoleApp.exe`
   - `Tools/esptool.exe`
   - all firmware files matching the target tag inside `Firmware/`
   - a `<binary>.manifest.json` sidecar for every firmware file
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
- every firmware project's merged release artifacts in `Firmware/`
- every merged binary's manifest sidecar in `Firmware/`
- every firmware file in that folder must match the target tag for release builds

When notes are requested, the text should follow the English structure defined by `release-notes-github`.

## Resources

- See `references/subskills.md` for the division of responsibilities.
