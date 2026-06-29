---
name: release-notes-github
description: Draft or update GitHub release notes for the LumaBloom release flow in this repository in a fixed English markdown format. Use when Codex needs to prepare a new release description and the request specifies both a source tag and a target tag, generate release links, or describe the expected versioned artifact names. Do not use this skill itself to build the firmware binary or Windows portable zip.
---

# Release Notes GitHub

Create release notes by reusing the repo's established structure instead of inventing a new format. The final release text should be in English.

## Workflow

1. Start from `references/release-template.md`.
2. Require the user request or the working prompt to include both tags:
   - source tag: from which previous release the changes are described
   - target tag: for which new release the text and links are produced
3. Confirm current firmware behavior against source docs before copying technical claims:
   - `firmware/firmware_esp32c6/README.md`
   - `README.md`
   - `docs/build-and-run.md`
4. If the user gives new artifact names, tags, or links, update them everywhere consistently.
5. Write the final release description in English.

## Required Output Shape

Keep this section order unless the user asks otherwise:

1. `## Included In This Release`
2. `## Release Files`
3. `## What Changed`
4. `## Supported Hardware`
5. `## How To Flash`
6. `## Telemetry`

Use short bullet lists and fenced code blocks where the template already uses them.

## Tag And Artifact Rules

- Always state both tags in the working context: `from <old-tag> to <new-tag>`.
- Replace the old tag in all release asset links with the target tag.
- Keep firmware links in the form:
  `https://github.com/mkdsfm/LumaBloom/releases/download/<tag>/<filename>`
- Include the target version in both release artifact filenames.
- Prefer these filename forms:
  - `luma_bloom_esp32c6_<to-tag>_merged.bin`
  - `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`
- If the release includes the Windows portable app, list the zip artifact before the firmware binary.
- If the user gives only filenames and no URLs, infer the URL from the tag and repository release pattern above.
- For the `## What Changed` section, describe the delta from the source tag to the target tag rather than writing a generic summary.

## Artifact References

- Use these expected filenames in the release note when artifacts are part of the release:
  - `luma_bloom_esp32c6_<to-tag>_merged.bin`
  - `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`
- If the user asks to actually build those artifacts, delegate that work to:
  - `.codex-skill-staging/esp32-release-flash`
  - `.codex-skill-staging/pc-app-portable-release`

## Technical Accuracy Rules

- Do not blindly copy telemetry semantics from an older release note.
- If repo docs say `value` is normalized or calibration is required, surface that instead of repeating an older raw-ADC claim.
- Preserve hardware wiring exactly when it is unchanged:
  - `VCC -> 3V3`
  - `GND -> GND`
  - `AO -> GPIO4`
- Keep the warning about avoiding `GPIO0` on `ESP32-C6` when it still matches the docs.

## Resources

- For the reusable release-note skeleton with placeholders, read `references/release-template.md`.

## Default Behavior

If the user asks to "write release notes" without more detail:

1. Start from the template in `references/release-template.md`.
2. Ask for the missing tag range if either the source tag or the target tag is absent.
3. Ask for missing artifact names only if they cannot be inferred safely.
