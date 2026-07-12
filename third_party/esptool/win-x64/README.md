Place the official standalone Windows x64 `esptool.exe` here before building the portable release zip.

Expected file:

- `third_party/esptool/win-x64/esptool.exe`

Why this exists:

- the portable release should bundle a self-contained `esptool.exe` for end users;
- copying `esptool.exe` from a Python virtual environment is not portable because that launcher depends on the original Python environment.

Recommended source:

- download the Windows x64 standalone binary from the official `esptool` release assets or installation docs.
