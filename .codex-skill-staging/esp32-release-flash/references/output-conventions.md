# Output Conventions

- Required project entrypoint: `firmware/<project>/build_merged.py`
- Output directory per project: `firmware/<project>/build/release/`
- Required filename suffix: `_<tag>_merged.bin`
- Required manifest: `<binary>.manifest.json` using schema version `1`
- Remove old binaries and manifests for the requested tag before building
- Default `<tag>` source: the MinVer version of `pc-app/BrightnessSensor.ConsoleApp`, normalized exactly like `AppVersion.Current`
- Current ESP32-C6 outputs:
  - `luma_bloom_esp32c6-display_<tag>_merged.bin`
  - `luma_bloom_esp32c6-touch_<tag>_merged.bin`
- Use the merged binary for releases and for full-device flashing at `0x0`
- Report every absolute output path back to the user
