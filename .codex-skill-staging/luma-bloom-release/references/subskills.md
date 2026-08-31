# Subskills

## `esp32-release-flash`

Use for:

- running every firmware project's own merged-binary script
- optional flashing of one explicitly selected ESP artifact

Expected output for LumaBloom releases:

- `firmware/<project>/build/release/*_<to-tag>_merged.bin` for every firmware project

## `pc-app-portable-release`

Use for:

- Windows portable `win-x64` publish
- packaging the publish folder into the versioned release zip
- bundling `Tools/esptool.exe` for the in-app Update flow
- bundling every firmware project's matching merged binaries and manifest sidecars in `Firmware/`

Expected output for LumaBloom releases:

- `luma-bloom-pc-app_<to-tag>_win-x64-portable.zip`

## `release-notes-github`

Use for:

- English release note drafting
- release link generation
- release section formatting
