# Output Conventions

- Default output directory: `build/release/`
- Default filename: `<project-name>_<idf-target>_merged.bin`
- User override: `--release-name <name>`
- If the override has no `.bin` suffix, append `.bin`
- Use the merged binary for releases and for full-device flashing at `0x0`
- Report the absolute output path back to the user
