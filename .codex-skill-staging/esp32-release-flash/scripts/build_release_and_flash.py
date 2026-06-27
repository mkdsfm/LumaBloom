#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def parse_cmake_cache(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    if not path.exists():
        return result
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if "=" not in line or line.startswith("//") or line.startswith("#"):
            continue
        key, value = line.split("=", 1)
        result[key.strip()] = value.strip()
    return result


def find_first_existing(paths: list[Path]) -> Path | None:
    for path in paths:
        if path.exists():
            return path
    return None


def find_latest(root: Path, pattern: str) -> Path | None:
    matches = sorted(root.glob(pattern))
    return matches[-1] if matches else None


def sanitize_name(raw: str) -> str:
    cleaned = raw.strip().lower()
    cleaned = re.sub(r"[^a-z0-9._-]+", "-", cleaned)
    cleaned = re.sub(r"-{2,}", "-", cleaned).strip("-")
    if not cleaned.endswith(".bin"):
        cleaned += ".bin"
    return cleaned


def run(cmd: list[str], cwd: Path, env: dict[str, str]) -> None:
    print(f"[run] {' '.join(cmd)}")
    subprocess.run(cmd, cwd=str(cwd), env=env, check=True)


def tool_env(project_dir: Path) -> tuple[dict[str, str], Path, Path, Path, Path, Path]:
    build_dir = project_dir / "build"
    config_env = read_json(build_dir / "config.env")
    cmake_cache = parse_cmake_cache(build_dir / "CMakeCache.txt")

    idf_path = Path(config_env["IDF_PATH"])
    python_exe = Path(cmake_cache.get("PYTHON:UNINITIALIZED", r"C:\Espressif\tools\python\v6.0\venv\Scripts\python.exe"))
    ninja_exe = Path(cmake_cache.get("CMAKE_MAKE_PROGRAM:FILEPATH", r"C:\Espressif\tools\ninja\1.12.1\ninja.exe"))
    cmake_exe = Path(cmake_cache.get("CMAKE_COMMAND:INTERNAL", r"C:\Espressif\tools\cmake\4.0.3\bin\cmake.exe"))

    tools_root = Path(r"C:\Espressif\tools")
    compiler_bin = find_latest(tools_root, r"riscv32-esp-elf\*\riscv32-esp-elf\bin")
    if compiler_bin is None:
        raise FileNotFoundError("ESP32 compiler toolchain not found under C:\\Espressif\\tools")

    esptool_exe = python_exe.parent / "esptool.exe"
    if not esptool_exe.exists():
        raise FileNotFoundError(f"esptool.exe not found next to {python_exe}")

    env = os.environ.copy()
    env["IDF_PATH"] = str(idf_path)
    env["IDF_TOOLS_PATH"] = str(tools_root)
    env["IDF_PYTHON_ENV_PATH"] = str(python_exe.parent.parent)
    env["ESP_IDF_VERSION"] = config_env.get("IDF_VERSION", "6.0.0")
    env["CMAKE_MAKE_PROGRAM"] = str(ninja_exe)
    env["ESP_ROM_ELF_DIR"] = str(idf_path / "components" / "esp_rom" / config_env["IDF_TARGET"])
    env["PATH"] = ";".join(
        [
            str(cmake_exe.parent),
            str(ninja_exe.parent),
            str(python_exe.parent),
            str(compiler_bin),
            env.get("PATH", ""),
        ]
    )
    env["GIT_CONFIG_COUNT"] = "3"
    env["GIT_CONFIG_KEY_0"] = "safe.directory"
    env["GIT_CONFIG_VALUE_0"] = str(idf_path).replace("\\", "/")
    env["GIT_CONFIG_KEY_1"] = "safe.directory"
    env["GIT_CONFIG_VALUE_1"] = str(project_dir).replace("\\", "/")
    env["GIT_CONFIG_KEY_2"] = "safe.directory"
    env["GIT_CONFIG_VALUE_2"] = str(idf_path / "components" / "openthread" / "openthread").replace("\\", "/")

    return env, idf_path, python_exe, ninja_exe, cmake_exe, esptool_exe


def ensure_bootloader(project_dir: Path, env: dict[str, str], idf_path: Path, python_exe: Path, ninja_exe: Path, cmake_exe: Path) -> None:
    build_dir = project_dir / "build"
    bootloader_dir = build_dir / "bootloader"
    sdkconfig = project_dir / "sdkconfig"

    cmd = [
        str(cmake_exe),
        f"-DSDKCONFIG={sdkconfig.as_posix()}",
        f"-DIDF_PATH={idf_path.as_posix()}",
        "-DIDF_TARGET=esp32c6",
        "-DPYTHON_DEPS_CHECKED=1",
        f"-DPYTHON={python_exe.as_posix()}",
        f"-DEXTRA_COMPONENT_DIRS={(idf_path / 'components' / 'bootloader').as_posix()}",
        f"-DPROJECT_SOURCE_DIR={project_dir.as_posix()}",
        "-DIGNORE_EXTRA_COMPONENT=",
        f"-DCMAKE_MAKE_PROGRAM={ninja_exe.as_posix()}",
        "-GNinja",
        "-S",
        (idf_path / "components" / "bootloader" / "subproject").as_posix(),
        "-B",
        bootloader_dir.as_posix(),
    ]
    run(cmd, project_dir, env)
    run([str(ninja_exe), "-C", str(bootloader_dir)], project_dir, env)


def build_app(project_dir: Path, env: dict[str, str], idf_path: Path, python_exe: Path) -> None:
    run([str(python_exe), str(idf_path / "tools" / "idf.py"), "app"], project_dir, env)


def merged_name(project_dir: Path, release_name: str | None) -> str:
    if release_name:
        return sanitize_name(release_name)

    description_path = project_dir / "build" / "project_description.json"
    project_name = project_dir.name
    target = "esp32"
    if description_path.exists():
        data = read_json(description_path)
        project_name = data.get("project_name", project_name)
        target = data.get("target", target)
    return sanitize_name(f"{project_name}_{target}_merged")


def flash_layout(project_dir: Path) -> tuple[str, str, str, str]:
    flasher_args = read_json(project_dir / "build" / "flasher_args.json")
    chip = flasher_args.get("extra_esptool_args", {}).get("chip", "esp32")
    flash_files = flasher_args["flash_files"]
    bootloader = flash_files["0x0"]
    partition = flash_files["0x8000"]
    app_offset, app_path = next((offset, path) for offset, path in flash_files.items() if offset not in {"0x0", "0x8000"})
    return chip, bootloader, partition, app_offset, app_path


def merge_binary(project_dir: Path, env: dict[str, str], esptool_exe: Path, output_name: str) -> Path:
    build_dir = project_dir / "build"
    output_dir = build_dir / "release"
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / output_name
    chip, bootloader_rel, partition_rel, app_offset, app_rel = flash_layout(project_dir)

    cmd = [
        str(esptool_exe),
        "--chip",
        chip,
        "merge-bin",
        "-o",
        str(output_path),
        "--flash-mode",
        "dio",
        "--flash-freq",
        "80m",
        "--flash-size",
        "2MB",
        "0x0",
        str(build_dir / bootloader_rel),
        "0x8000",
        str(build_dir / partition_rel),
        app_offset,
        str(build_dir / app_rel),
    ]
    run(cmd, project_dir, env)
    return output_path


def flash_binary(project_dir: Path, env: dict[str, str], esptool_exe: Path, binary_path: Path, port: str, baud: int) -> None:
    chip, _, _, _, _ = flash_layout(project_dir)
    cmd = [
        str(esptool_exe),
        "--chip",
        chip,
        "--port",
        port,
        "--baud",
        str(baud),
        "--before",
        "default-reset",
        "--after",
        "hard-reset",
        "write-flash",
        "0x0",
        str(binary_path),
    ]
    run(cmd, project_dir, env)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a readable merged ESP32 binary and optionally flash it.")
    parser.add_argument("--project", required=True, help="Path to the ESP-IDF project")
    parser.add_argument("--release-name", help="Readable output filename (.bin appended automatically if omitted)")
    parser.add_argument("--flash-port", help="COM port to flash after building, for example COM8")
    parser.add_argument("--baud", type=int, default=460800, help="Baud rate for flashing")
    parser.add_argument("--skip-build", action="store_true", help="Reuse existing build artifacts instead of rebuilding app and bootloader")
    args = parser.parse_args()

    project_dir = Path(args.project).resolve()
    env, idf_path, python_exe, ninja_exe, cmake_exe, esptool_exe = tool_env(project_dir)

    if not args.skip_build:
        build_app(project_dir, env, idf_path, python_exe)
        ensure_bootloader(project_dir, env, idf_path, python_exe, ninja_exe, cmake_exe)
    output_path = merge_binary(project_dir, env, esptool_exe, merged_name(project_dir, args.release_name))

    if args.flash_port:
        flash_binary(project_dir, env, esptool_exe, output_path, args.flash_port, args.baud)

    print(f"[ok] merged binary: {output_path}")
    if args.flash_port:
        print(f"[ok] flashed to: {args.flash_port}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
