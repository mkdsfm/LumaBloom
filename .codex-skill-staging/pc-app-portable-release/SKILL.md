---
name: pc-app-portable-release
description: Build the Windows portable zip artifact for this repository with a versioned LumaBloom release filename. Use when Codex needs to publish `pc-app` as a self-contained `win-x64` folder, package it into `luma-bloom-pc-app_<tag>_win-x64-portable.zip`, or regenerate the release zip for a specific target tag.
---

# PC App Portable Release

Build the Windows portable release artifact for `pc-app/` and package it into the repo's expected versioned zip format.

## Workflow

1. Require a target tag such as `0.3.0`.
2. Run `scripts/build_portable_zip.py` with that tag.
3. Report:
   - the publish output folder
   - the final zip path
   - the exact release filename
4. If `dotnet publish` is blocked by sandbox access to `NuGet.Config` or package caches, rerun with escalation.

## Commands

### Build the versioned portable zip

```powershell
python scripts/build_portable_zip.py --tag 0.3.0
```

### Build for a custom project root

```powershell
python scripts/build_portable_zip.py --tag 0.3.0 --repo-root C:\path\to\repo
```

## Naming Rules

- The output zip must be named:
  `luma-bloom-pc-app_<tag>_win-x64-portable.zip`
- The publish folder remains:
  `pc-app/artifacts/portable/win-x64/`
- The zip file is written to:
  `pc-app/artifacts/portable/`

## Resources

- Use `scripts/build_portable_zip.py` for the deterministic build-and-package flow.
- See `references/output-conventions.md` for paths and naming rules.
