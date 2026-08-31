# Output Conventions

- Publish folder:
  `pc-app/artifacts/single-file/win-x64/`
- Final zip folder:
  `pc-app/artifacts/single-file/`
- Final zip filename:
  `luma-bloom-pc-app_<tag>_win-x64-portable.zip`
- Target runtime:
  `win-x64`
- Publish mode:
  self-contained single-file
- Standalone executable:
  `pc-app/artifacts/single-file/win-x64/BrightnessSensor.ConsoleApp.exe`
- Bundled flashing tool:
  `pc-app/artifacts/single-file/win-x64/Tools/esptool.exe`
- Required firmware source directories:
  `firmware/<project>/build/release/`
- Bundled firmware release contents:
  `pc-app/artifacts/single-file/win-x64/Firmware/<release-files>`
- Every immediate firmware project directory must provide `build_merged.py`, at least one matching `*_<tag>_merged.bin`, and a `<binary>.manifest.json` sidecar for each binary.
- Main project:
  `pc-app/BrightnessSensor.ConsoleApp/BrightnessSensor.ConsoleApp.csproj`
