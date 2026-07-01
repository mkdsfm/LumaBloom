---
name: pc-app-portable-release
description: Build the Windows portable zip artifact for this repository with a versioned LumaBloom release filename. Use when Codex needs to run the documented repo-root portable build flow for `pc-app`, produce the self-contained single-file `win-x64` output, package it into `luma-bloom-pc-app_<tag>_win-x64-portable.zip`, or regenerate the release zip for a specific target tag.
---

# PC App Portable Release

Build the Windows portable release artifact for `pc-app/` using the flow documented in `docs/build-and-run.md`, then package it into the repo's expected versioned zip format.

## Workflow

1. Require a target tag such as `0.3.0`.
2. From the repo root, run `.codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py` with that tag.
3. Treat the direct `dotnet publish` single-file command as the lower-level equivalent documented in `docs/build-and-run.md`.
4. Report:
   - the publish output folder
   - the final zip path
   - the exact release filename
5. If `dotnet publish` is blocked by sandbox access to `NuGet.Config` or package caches, rerun with escalation.

## Commands

### Build the versioned portable zip

```powershell
python .codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py --tag 0.3.0
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
- The publish folder remains:
  `pc-app/artifacts/single-file/win-x64/`
- The zip file is written to:
  `pc-app/artifacts/single-file/`
- The single-file executable is:
  `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`

## Resources

- Use `.codex-skill-staging/pc-app-portable-release/scripts/build_portable_zip.py` from the repo root for the documented build-and-package flow.
- See `references/output-conventions.md` for paths and naming rules.
