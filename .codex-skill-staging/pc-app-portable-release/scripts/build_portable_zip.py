#!/usr/bin/env python3
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def run(cmd: list[str], cwd: Path) -> None:
    print(f"[run] {' '.join(cmd)}")
    subprocess.run(cmd, cwd=str(cwd), check=True)


def read_product_version(executable_path: Path) -> str:
    escaped_path = str(executable_path).replace("'", "''")
    command = [
        "powershell",
        "-NoProfile",
        "-Command",
        f"[System.Diagnostics.FileVersionInfo]::GetVersionInfo('{escaped_path}').ProductVersion",
    ]
    result = subprocess.run(
        command,
        check=True,
        cwd=str(executable_path.parent),
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def zip_dir(source_dir: Path, zip_path: Path, root_name: str) -> None:
    if zip_path.exists():
        zip_path.unlink()
    archive_base = zip_path.with_suffix("")
    temp_root = source_dir.parent / f"__zip_staging__{root_name}"
    if temp_root.exists():
        shutil.rmtree(temp_root)
    shutil.copytree(source_dir, temp_root)
    try:
        shutil.make_archive(str(archive_base), "zip", root_dir=temp_root.parent, base_dir=temp_root.name)
    finally:
        shutil.rmtree(temp_root)


def find_firmware_release_dir(repo_root: Path) -> tuple[Path | None, str]:
    release_dir = repo_root / "firmware" / "firmware_esp32c6" / "build" / "release"
    if not release_dir.exists():
        return None, "firmware release directory not found"

    files = [path for path in release_dir.iterdir() if path.is_file()]
    if not files:
        return None, "firmware release directory is empty"

    return release_dir, "directory"


def should_require_exact_firmware_tag(tag: str) -> bool:
    normalized = tag.strip().lower()
    return normalized not in {"", "dev", "local", "snapshot"}


def resolve_firmware_bundle_files(repo_root: Path, tag: str) -> tuple[list[Path], str]:
    firmware_release_dir, resolution = find_firmware_release_dir(repo_root)
    if firmware_release_dir is None:
        return [], resolution

    version_matches = sorted(
        path
        for path in firmware_release_dir.iterdir()
        if path.is_file() and tag in path.name and "merged" in path.name
    )
    if version_matches:
        return version_matches, "version-matched merged bin"

    if should_require_exact_firmware_tag(tag):
        raise RuntimeError(
            "firmware release directory does not contain a merged bin matching the requested tag "
            f"'{tag}'. Build the firmware release payload first so the portable package cannot bundle an older file."
        )

    merged_bins = sorted(
        path for path in firmware_release_dir.iterdir() if path.is_file() and "merged" in path.name
    )
    if merged_bins:
        return [merged_bins[-1]], "latest merged bin"

    return sorted(path for path in firmware_release_dir.iterdir() if path.is_file()), resolution

def copy_firmware_bundle(repo_root: Path, publish_dir: Path, tag: str) -> None:
    firmware_files, resolution = resolve_firmware_bundle_files(repo_root, tag)
    if not firmware_files:
        print(f"[warn] skipped firmware bundle: {resolution}")
        return

    firmware_dir = publish_dir / "Firmware"
    if firmware_dir.exists():
        shutil.rmtree(firmware_dir)
    firmware_dir.mkdir(parents=True, exist_ok=True)
    for source in firmware_files:
        shutil.copy2(source, firmware_dir / source.name)
    print(f"[ok] bundled firmware release folder: {firmware_dir} ({resolution})")


def validate_publish_output(publish_dir: Path, tag: str) -> None:
    executable_path = publish_dir / "BrightnessSensor.ConsoleApp.exe"
    if not executable_path.exists():
        raise RuntimeError(f"published executable not found: {executable_path}")

    product_version = read_product_version(executable_path)
    if product_version != tag:
        raise RuntimeError(
            f"published executable version mismatch: expected '{tag}', got '{product_version}'. "
            "This would ship a preview or wrong-version app package."
        )

    print(f"[ok] validated executable version: {product_version}")


def resolve_esptool_source(repo_root: Path) -> tuple[Path | None, str]:
    candidates = [
        (
            repo_root / "third_party" / "esptool" / "win-x64" / "esptool.exe",
            "repo-local standalone bundle",
        ),
    ]

    for candidate, description in candidates:
        if candidate.exists():
            return candidate, description

    return None, "esptool.exe not found"


def copy_esptool(repo_root: Path, publish_dir: Path) -> None:
    esptool_source, resolution = resolve_esptool_source(repo_root)
    if esptool_source is None:
        print(
            "[warn] skipped esptool bundle: place the official standalone Windows binary at "
            "third_party\\esptool\\win-x64\\esptool.exe"
        )
        return

    tools_dir = publish_dir / "Tools"
    tools_dir.mkdir(parents=True, exist_ok=True)
    esptool_target = tools_dir / "esptool.exe"
    shutil.copy2(esptool_source, esptool_target)
    print(f"[ok] bundled esptool: {esptool_target} ({resolution})")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build the documented repo-root portable Windows zip for pc-app using the single-file publish output."
    )
    parser.add_argument("--tag", required=True, help="Target release tag, for example 0.3.0")
    parser.add_argument("--repo-root", default=".", help="Repository root")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    pc_app_root = repo_root / "pc-app"
    project = pc_app_root / "BrightnessSensor.ConsoleApp" / "BrightnessSensor.ConsoleApp.csproj"
    publish_dir = pc_app_root / "artifacts" / "single-file" / "win-x64"
    zip_name = f"luma-bloom-pc-app_{args.tag}_win-x64-portable.zip"
    zip_path = pc_app_root / "artifacts" / "single-file" / zip_name

    if publish_dir.exists():
        shutil.rmtree(publish_dir)
    publish_dir.mkdir(parents=True, exist_ok=True)
    zip_path.parent.mkdir(parents=True, exist_ok=True)

    run(
        [
            "dotnet",
            "publish",
            str(project),
            "-c",
            "Release",
            "-r",
            "win-x64",
            "--self-contained",
            "true",
            "-o",
            str(publish_dir),
            f"/p:MinVerVersionOverride={args.tag}",
            f"/p:Version={args.tag}",
            f"/p:AssemblyVersion={args.tag}.0",
            f"/p:FileVersion={args.tag}.0",
            f"/p:InformationalVersion={args.tag}",
            f"/p:AssemblyInformationalVersion={args.tag}",
            "/p:IncludeSourceRevisionInInformationalVersion=false",
            "/p:PublishSingleFile=true",
            "/p:IncludeNativeLibrariesForSelfExtract=true",
            "/p:EnableCompressionInSingleFile=true",
            "/p:DebugType=None",
            "/p:DebugSymbols=false",
        ],
        repo_root,
    )

    copy_firmware_bundle(repo_root, publish_dir, args.tag)
    copy_esptool(repo_root, publish_dir)
    validate_publish_output(publish_dir, args.tag)

    zip_dir(publish_dir, zip_path, f"luma-bloom-pc-app_{args.tag}_win-x64")

    print(f"[ok] publish folder: {publish_dir}")
    print(f"[ok] portable zip: {zip_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
