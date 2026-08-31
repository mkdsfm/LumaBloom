---
name: pc-app-portable-release
description: Build the versioned LumaBloom Windows portable zip, first running every firmware project's own merged-binary build script and then bundling all matching firmware artifacts with the app and flashing tool. Use for standalone PC packages or full portable release artifacts.
---

# PC App Portable Release

Build the Windows portable release artifact for `pc-app/` using the flow documented in `docs/build-and-run.md`, then package it into the repo's expected versioned zip format.

## Workflow

1. By default, resolve the target version from the same MinVer configuration used by `pc-app`. Pass `--tag` only for an explicit release-version override.
2. From the repo root, run `.codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py`. It discovers every directory under `firmware/`, requires its own `build_merged.py`, and runs every script with the resolved version before publishing the PC app.
3. Treat the direct `dotnet publish` single-file command as the lower-level equivalent documented in `docs/build-and-run.md`.
4. Report:
   - the publish output folder
   - the final zip path
   - the exact release filename
   - whether `Tools/esptool.exe` was bundled
   - every firmware artifact bundled into `Firmware/`
5. Treat this release build as strict:
   - the published `BrightnessSensor.ConsoleApp.exe` must report the exact requested tag, not a `preview` or other prerelease suffix
   - every firmware project must contribute artifacts and manifests matching the resolved version; never fall back to an older file or omit a project
6. If `dotnet publish` is blocked by sandbox access to `NuGet.Config` or package caches, rerun with escalation.

## Commands

### Build the versioned portable zip

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py
```

### Build the single-file publish output directly

```powershell
dotnet publish pc-app/BrightnessSensor.ConsoleApp/BrightnessSensor.ConsoleApp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o pc-app/artifacts/single-file/win-x64 `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=None `
  /p:DebugSymbols=false
```

### Build for a custom project root

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag 0.3.0 --repo-root C:\path\to\repo
```

## Naming Rules

- The output zip must be named:
  `luma-bloom-pc-app_<tag>_win-x64-portable.zip`
- By default, `<tag>` is the MinVer version used by `AppVersion.Current`; `--tag` overrides it for an explicit release.
- The publish folder remains:
  `pc-app/artifacts/single-file/win-x64/`
- The zip file is written to:
  `pc-app/artifacts/single-file/`
- The single-file executable is:
  `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`
- The bundled flashing tool is:
  `pc-app/artifacts/single-file/win-x64/Tools/esptool.exe`
- Firmware projects write their payloads to `firmware/<project>/build/release/`; all matching artifacts are copied to:
  `pc-app/artifacts/single-file/win-x64/Firmware/<release-files>`
- Each copied binary must include its `<binary>.manifest.json` sidecar.
- For release tags, fail the build instead of silently bundling the wrong firmware version or omitting a firmware project.
- `--skip-firmware-build` is reserved for the top-level release coordinator after it has already built every firmware project successfully.

## Resources

- Use `.codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py` from the repo root for the documented build-and-package flow.
- See `references/output-conventions.md` for paths and naming rules.
