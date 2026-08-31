#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path


def run(command: list[str], cwd: Path, dry_run: bool = False) -> None:
    print(f"[run] {subprocess.list2cmdline(command)}")
    if not dry_run:
        subprocess.run(command, cwd=str(cwd), check=True)


def resolve_pc_app_version(repo_root: Path) -> str:
    project = repo_root / "pc-app" / "BrightnessSensor.ConsoleApp" / "BrightnessSensor.ConsoleApp.csproj"
    command = ["dotnet", "msbuild", str(project), "-t:MinVer", "-p:MinVerVerbosity=detailed", "-v:minimal"]
    result = subprocess.run(command, cwd=str(repo_root), check=True, capture_output=True, text=True)
    match = re.search(r"MinVerVersion=([^\s]+)", result.stdout)
    if match is None:
        raise RuntimeError("MinVer did not report the pc-app version")
    return match.group(1).split("+", 1)[0]


def read_build_python(cache_path: Path) -> Path | None:
    if not cache_path.exists():
        return None
    for line in cache_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if line.startswith(("PYTHON:UNINITIALIZED=", "PYTHON:FILEPATH=")):
            candidate = Path(line.split("=", 1)[1])
            if candidate.exists():
                return candidate
    return None


def resolve_esptool(repo_root: Path) -> list[str]:
    standalone = repo_root / "third_party" / "esptool" / "win-x64" / "esptool.exe"
    if standalone.exists():
        return [str(standalone)]

    executable = shutil.which("esptool.exe") or shutil.which("esptool.py") or shutil.which("esptool")
    if executable:
        return [executable]

    for cache_path in sorted((repo_root / "firmware").glob("*/build/CMakeCache.txt")):
        python_executable = read_build_python(cache_path)
        if python_executable is None:
            continue
        sibling = python_executable.parent / "esptool.exe"
        if sibling.exists():
            return [str(sibling)]

    raise FileNotFoundError("esptool was not found in third_party, PATH, or an ESP-IDF build environment")


def collect_artifacts(repo_root: Path, tag: str) -> list[Path]:
    return sorted(path for path in (repo_root / "firmware").glob(f"*/build/release/*_{tag}_merged.bin") if path.is_file())


def manifest_path(binary_path: Path) -> Path:
    return Path(f"{binary_path}.manifest.json")


def read_manifest(binary_path: Path) -> dict:
    path = manifest_path(binary_path)
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1 or data.get("fileName") != binary_path.name:
        raise RuntimeError(f"invalid firmware artifact manifest: {path}")
    return data


def clean_release_artifacts(build_script: Path, tag: str, dry_run: bool) -> None:
    release_dir = build_script.parent / "build" / "release"
    for binary in release_dir.glob(f"*_{tag}_merged.bin") if release_dir.exists() else []:
        for path in (binary, manifest_path(binary)):
            if path.exists():
                print(f"[clean] {path}")
                if not dry_run:
                    path.unlink()


def firmware_build_scripts(repo_root: Path) -> list[Path]:
    firmware_root = repo_root / "firmware"
    projects = sorted(path for path in firmware_root.iterdir() if path.is_dir() and not path.name.startswith("."))
    missing = [project.name for project in projects if not (project / "build_merged.py").is_file()]
    if missing:
        raise RuntimeError(f"firmware projects missing required build_merged.py: {', '.join(missing)}")
    if not projects:
        raise RuntimeError(f"no firmware projects found under {firmware_root}")
    return [project / "build_merged.py" for project in projects]


def select_flash_artifact(artifacts: list[Path], requested: str | None, repo_root: Path) -> Path:
    if requested:
        direct = Path(requested)
        if not direct.is_absolute():
            direct = repo_root / direct
        if direct.is_file():
            return direct.resolve()
        matches = [path for path in artifacts if path.name == requested]
        if len(matches) == 1:
            return matches[0]
        raise FileNotFoundError(f"requested firmware artifact was not found: {requested}")

    if len(artifacts) != 1:
        choices = "\n".join(f"  - {path}" for path in artifacts)
        raise RuntimeError(f"select exactly one artifact with --firmware-file before flashing:\n{choices}")
    return artifacts[0]


def main() -> int:
    parser = argparse.ArgumentParser(description="Build every repository firmware release binary and optionally flash one selected artifact.")
    parser.add_argument("--tag", help="Version included in all generated firmware filenames; defaults to the pc-app MinVer version")
    parser.add_argument("--repo-root", default=".", help="Repository root")
    parser.add_argument("--skip-build", action="store_true", help="Merge existing build artifacts without rebuilding firmware")
    parser.add_argument("--flash-port", help="COM port to flash after all firmware artifacts are built")
    parser.add_argument("--firmware-file", help="Exact merged binary path or filename to flash; required when multiple artifacts exist")
    parser.add_argument("--dry-run", action="store_true", help="Print firmware build commands without executing them")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    tag = args.tag or resolve_pc_app_version(repo_root)
    print(f"[version] {tag}")
    for build_script in firmware_build_scripts(repo_root):
        clean_release_artifacts(build_script, tag, args.dry_run)
        command = [sys.executable, str(build_script), "--tag", tag]
        if args.skip_build:
            command.append("--skip-build")
        if args.dry_run:
            command.append("--dry-run")
        run(command, repo_root)

    if args.dry_run:
        return 0

    artifacts = collect_artifacts(repo_root, tag)
    if not artifacts:
        raise RuntimeError(f"no merged firmware artifacts were created for tag '{tag}'")
    for artifact in artifacts:
        if not manifest_path(artifact).is_file():
            raise RuntimeError(f"firmware artifact manifest was not created: {manifest_path(artifact)}")
        print(f"[ok] firmware artifact: {artifact}")
        print(f"[ok] firmware manifest: {manifest_path(artifact)}")

    if args.flash_port:
        artifact = select_flash_artifact(artifacts, args.firmware_file, repo_root)
        manifest = read_manifest(artifact)
        if manifest.get("flashMethod") != "esptool":
            raise RuntimeError(f"firmware artifact does not support esptool flashing: {artifact}")
        esptool = resolve_esptool(repo_root)
        run(esptool + ["--chip", str(manifest["chip"]), "--port", args.flash_port, "--baud", str(manifest["baudRate"]), "write-flash", str(manifest["offset"]), str(artifact)], repo_root)
        print(f"[ok] flashed {artifact.name} to {args.flash_port}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
