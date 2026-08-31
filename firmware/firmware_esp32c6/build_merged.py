#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class FirmwareVariant:
    variant_id: str
    display_type: int
    board: str


VARIANTS = (
    FirmwareVariant("esp32c6-display", 1, "waveshare-esp32-c6-lcd-1.47"),
    FirmwareVariant("esp32c6-touch", 2, "waveshare-esp32-c6-touch-lcd-1.47"),
)


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def parse_cmake_cache(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    if not path.exists():
        return result
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if "=" not in line or line.startswith(("//", "#")):
            continue
        key, value = line.split("=", 1)
        result[key.strip()] = value.strip()
    return result


def sanitize_tag(value: str) -> str:
    sanitized = re.sub(r"[^a-zA-Z0-9._-]+", "-", value.strip()).strip("-.")
    if not sanitized:
        raise ValueError("tag must contain at least one filename-safe character")
    return sanitized


def find_idf_path(project_dir: Path) -> Path | None:
    environment_path = os.environ.get("IDF_PATH")
    if environment_path and (Path(environment_path) / "tools" / "idf.py").exists():
        return Path(environment_path)

    for config_path in (project_dir / "build" / "config.env", project_dir / "build_touch" / "config.env"):
        if config_path.exists():
            candidate = Path(read_json(config_path)["IDF_PATH"])
            if (candidate / "tools" / "idf.py").exists():
                return candidate

    candidates = sorted(Path("C:/esp").glob("*/esp-idf/tools/idf.py"), reverse=True)
    return candidates[0].parent.parent if candidates else None


def find_idf_python(project_dir: Path) -> Path | None:
    environment_path = os.environ.get("IDF_PYTHON_ENV_PATH")
    if environment_path:
        environment_root = Path(environment_path)
        for candidate in (environment_root / "Scripts" / "python.exe", environment_root / "bin" / "python"):
            if candidate.exists():
                return candidate

    for cache_path in (project_dir / "build" / "CMakeCache.txt", project_dir / "build_touch" / "CMakeCache.txt"):
        cache = parse_cmake_cache(cache_path)
        for key in ("PYTHON:UNINITIALIZED", "PYTHON:FILEPATH"):
            candidate_value = cache.get(key)
            if candidate_value and Path(candidate_value).exists():
                return Path(candidate_value)
    return None


def resolve_idf_command(project_dir: Path, idf_py_override: str | None, idf_python_override: str | None, dry_run: bool) -> list[str]:
    if idf_py_override:
        idf_py = Path(idf_py_override).resolve()
        if not idf_py.exists():
            raise FileNotFoundError(f"idf.py not found: {idf_py}")
    else:
        executable = shutil.which("idf.py")
        if executable:
            return [executable]
        idf_path = find_idf_path(project_dir)
        if idf_path is None:
            if dry_run:
                return ["idf.py"]
            raise FileNotFoundError("idf.py was not found. Run from an ESP-IDF shell or pass --idf-py.")
        idf_py = idf_path / "tools" / "idf.py"

    if idf_python_override:
        python_executable = Path(idf_python_override).resolve()
        if not python_executable.exists():
            raise FileNotFoundError(f"ESP-IDF Python was not found: {python_executable}")
    else:
        python_executable = find_idf_python(project_dir) or Path(sys.executable)
    return [str(python_executable), str(idf_py)]


def latest_directory(root: Path, pattern: str) -> Path | None:
    matches = sorted(root.glob(pattern))
    return matches[-1] if matches else None


def prepare_environment(project_dir: Path, idf_command: list[str]) -> dict[str, str]:
    environment = os.environ.copy()
    if len(idf_command) < 2 or not idf_command[-1].lower().endswith("idf.py"):
        return environment

    idf_path = Path(idf_command[-1]).parent.parent
    python_executable = Path(idf_command[0])
    if not python_executable.exists():
        return environment

    tools_root = python_executable.parents[4]
    tool_directories = [
        latest_directory(tools_root, "cmake/*/bin"),
        latest_directory(tools_root, "ninja/*"),
        latest_directory(tools_root, "riscv32-esp-elf/*/riscv32-esp-elf/bin"),
        python_executable.parent,
    ]
    environment["IDF_PATH"] = str(idf_path)
    environment["IDF_TOOLS_PATH"] = str(tools_root)
    environment["IDF_PYTHON_ENV_PATH"] = str(python_executable.parent.parent)
    environment["ESP_IDF_VERSION"] = "6.0.0"
    environment["ESP_ROM_ELF_DIR"] = str(idf_path / "components" / "esp_rom" / "esp32c6")
    environment["PATH"] = os.pathsep.join([str(path) for path in tool_directories if path is not None] + [environment.get("PATH", "")])
    environment["GIT_CONFIG_COUNT"] = "3"
    environment["GIT_CONFIG_KEY_0"] = "safe.directory"
    environment["GIT_CONFIG_VALUE_0"] = idf_path.as_posix()
    environment["GIT_CONFIG_KEY_1"] = "safe.directory"
    environment["GIT_CONFIG_VALUE_1"] = project_dir.as_posix()
    environment["GIT_CONFIG_KEY_2"] = "safe.directory"
    environment["GIT_CONFIG_VALUE_2"] = (idf_path / "components" / "openthread" / "openthread").as_posix()
    return environment


def run(command: list[str], cwd: Path, environment: dict[str, str], dry_run: bool) -> None:
    print(f"[run] {subprocess.list2cmdline(command)}")
    if not dry_run:
        subprocess.run(command, cwd=str(cwd), env=environment, check=True)


def manifest_path(binary_path: Path) -> Path:
    return Path(f"{binary_path}.manifest.json")


def clean_release_artifacts(project_dir: Path, variants: list[FirmwareVariant], tag: str, targeted: bool, dry_run: bool) -> None:
    release_dir = project_dir / "build" / "release"
    binaries = [release_dir / f"luma_bloom_{variant.variant_id}_{tag}_merged.bin" for variant in variants]
    if not targeted and release_dir.exists():
        binaries = list(release_dir.glob(f"*_{tag}_merged.bin"))

    for binary in binaries:
        for path in (binary, manifest_path(binary)):
            if path.exists():
                print(f"[clean] {path}")
                if not dry_run:
                    path.unlink()


def resolve_esptool_command(idf_command: list[str]) -> list[str]:
    if len(idf_command) > 1 and idf_command[-1].lower().endswith("idf.py"):
        return [idf_command[0], "-m", "esptool"]

    executable = shutil.which("esptool.exe") or shutil.which("esptool.py") or shutil.which("esptool")
    return [executable] if executable else [sys.executable, "-m", "esptool"]


def write_manifest(binary_path: Path, variant: FirmwareVariant, tag: str) -> Path:
    path = manifest_path(binary_path)
    data = {
        "schemaVersion": 1,
        "version": tag,
        "fileName": binary_path.name,
        "variant": variant.variant_id,
        "board": variant.board,
        "flashMethod": "esptool",
        "chip": "esp32c6",
        "baudRate": 460800,
        "offset": "0x0",
    }
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return path


def build_variant(project_dir: Path, idf_command: list[str], esptool_command: list[str], environment: dict[str, str], variant: FirmwareVariant, tag: str, skip_build: bool, dry_run: bool) -> tuple[Path, Path]:
    build_dir = project_dir / "build" / "variants" / variant.variant_id
    output_path = project_dir / "build" / "release" / f"luma_bloom_{variant.variant_id}_{tag}_merged.bin"

    if not skip_build:
        run(idf_command + ["-B", str(build_dir), "-D", f"APP_DISPLAY_TYPE={variant.display_type}", "-D", f"PROJECT_VER={tag}", "build"], project_dir, environment, dry_run)
    elif not (build_dir / "flash_args").is_file():
        raise FileNotFoundError(f"existing build artifacts were not found for {variant.variant_id}: {build_dir / 'flash_args'}")

    if not dry_run:
        output_path.parent.mkdir(parents=True, exist_ok=True)
    run(esptool_command + ["--chip", "esp32c6", "merge-bin", "-o", str(output_path), "-f", "raw", "@flash_args"], build_dir, environment, dry_run)
    manifest = manifest_path(output_path) if dry_run else write_manifest(output_path, variant, tag)
    return output_path, manifest


def main() -> int:
    parser = argparse.ArgumentParser(description="Build merged binaries for every supported ESP32-C6 LumaBloom variant.")
    parser.add_argument("--tag", default="dev", help="Version included in generated filenames")
    parser.add_argument("--variant", action="append", choices=[variant.variant_id for variant in VARIANTS], help="Build only the selected variant; repeat for more than one")
    parser.add_argument("--skip-build", action="store_true", help="Merge existing variant build artifacts without rebuilding")
    parser.add_argument("--idf-py", help="Explicit path to ESP-IDF tools/idf.py")
    parser.add_argument("--idf-python", help="Explicit path to the ESP-IDF Python executable")
    parser.add_argument("--dry-run", action="store_true", help="Print build and merge commands without executing them")
    parser.add_argument("--list", action="store_true", help="List supported firmware variants and exit")
    args = parser.parse_args()

    selected_ids = set(args.variant or [])
    variants = [variant for variant in VARIANTS if not selected_ids or variant.variant_id in selected_ids]
    if args.list:
        for variant in variants:
            print(variant.variant_id)
        return 0

    project_dir = Path(__file__).resolve().parent
    tag = sanitize_tag(args.tag)
    idf_command = resolve_idf_command(project_dir, args.idf_py, args.idf_python, args.dry_run)
    esptool_command = resolve_esptool_command(idf_command)
    environment = prepare_environment(project_dir, idf_command)
    clean_release_artifacts(project_dir, variants, tag, targeted=bool(args.variant), dry_run=args.dry_run)
    artifacts = [build_variant(project_dir, idf_command, esptool_command, environment, variant, tag, args.skip_build, args.dry_run) for variant in variants]

    for binary, manifest in artifacts:
        print(f"[artifact] {binary}")
        print(f"[manifest] {manifest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
