---
name: luma-bloom-release
description: Coordinate the full LumaBloom release workflow for this repository. Use when Codex needs to produce the versioned firmware binary, the versioned Windows portable zip, and the English release notes and the request specifies both a source tag and a target tag.
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
   build `luma_bloom_esp32c6_<to-tag>_merged.bin`
2. `.codex-skill-staging/pc-app-portable-release`
   Responsibility:
   build `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`
3. `.codex-skill-staging/release-notes-github`
   Responsibility:
   write the English GitHub release description for `<from-tag> -> <to-tag>`

## Workflow

1. Confirm the tag range.
2. If the user wants release artifacts, build the firmware binary through `esp32-release-flash`.
3. If the user wants release artifacts, build the Windows portable zip through `pc-app-portable-release`.
4. Draft the release notes through `release-notes-github`.
5. Report:
   - the final artifact filenames
   - the final artifact paths
   - the completed release note text

## Output Contract

When artifacts are requested, the release should end with these filenames:

- `luma_bloom_esp32c6_<to-tag>_merged.bin`
- `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`

When notes are requested, the text should follow the English structure defined by `release-notes-github`.

## Resources

- See `references/subskills.md` for the division of responsibilities.
