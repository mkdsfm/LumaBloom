#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
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
    return result.stdout.strip().split("+", 1)[0]


def resolve_pc_app_version(repo_root: Path) -> str:
    project = repo_root / "pc-app" / "BrightnessSensor.ConsoleApp" / "BrightnessSensor.ConsoleApp.csproj"
    command = ["dotnet", "msbuild", str(project), "-t:MinVer", "-p:MinVerVerbosity=detailed", "-v:minimal"]
    result = subprocess.run(command, cwd=str(repo_root), check=True, capture_output=True, text=True)
    match = re.search(r"MinVerVersion=([^\s]+)", result.stdout)
    if match is None:
        raise RuntimeError("MinVer did not report the pc-app version")
    return match.group(1).split("+", 1)[0]


def numeric_file_version(version: str) -> str:
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)(?:[-+].*)?", version)
    if match is None:
        raise ValueError(f"version must start with major.minor.patch: {version}")
    return f"{match.group(1)}.{match.group(2)}.{match.group(3)}.0"


def zip_dir(source_dir: Path, zip_path: Path, root_name: str) -> None:
    if zip_path.exists():
        zip_path.unlink()
    archive_base = zip_path.with_suffix("")
    temp_parent = source_dir.parent / "__zip_staging__"
    temp_root = temp_parent / root_name
    if temp_parent.exists():
        shutil.rmtree(temp_parent)
    shutil.copytree(source_dir, temp_root)
    try:
        shutil.make_archive(str(archive_base), "zip", root_dir=temp_parent, base_dir=root_name)
    finally:
        shutil.rmtree(temp_parent)


def firmware_projects(repo_root: Path) -> list[Path]:
    firmware_root = repo_root / "firmware"
    return sorted(path for path in firmware_root.iterdir() if path.is_dir() and not path.name.startswith("."))


def manifest_path(binary_path: Path) -> Path:
    return Path(f"{binary_path}.manifest.json")


def clean_release_artifacts(project: Path, tag: str) -> None:
    release_dir = project / "build" / "release"
    for binary in release_dir.glob(f"*_{tag}_merged.bin") if release_dir.exists() else []:
        for path in (binary, manifest_path(binary)):
            if path.exists():
                print(f"[clean] {path}")
                path.unlink()


def build_all_firmware(repo_root: Path, tag: str) -> None:
    projects = firmware_projects(repo_root)
    missing = [project.name for project in projects if not (project / "build_merged.py").is_file()]
    if missing:
        raise RuntimeError(f"firmware projects missing required build_merged.py: {', '.join(missing)}")
    if not projects:
        raise RuntimeError(f"no firmware projects found under {repo_root / 'firmware'}")
    for project in projects:
        clean_release_artifacts(project, tag)
        run([sys.executable, str(project / "build_merged.py"), "--tag", tag], repo_root)


def should_require_exact_firmware_tag(tag: str) -> bool:
    normalized = tag.strip().lower()
    return normalized not in {"", "dev", "local", "snapshot"}


def resolve_firmware_bundle_files(repo_root: Path, tag: str) -> tuple[list[Path], str]:
    projects = firmware_projects(repo_root)
    version_matches: list[Path] = []
    missing_projects: list[str] = []
    for project in projects:
        release_dir = project / "build" / "release"
        matches = sorted(path for path in release_dir.glob(f"*_{tag}_merged.bin") if path.is_file()) if release_dir.exists() else []
        missing_manifests = [manifest_path(path) for path in matches if not manifest_path(path).is_file()]
        if missing_manifests:
            raise RuntimeError(f"firmware artifact manifests are missing: {', '.join(str(path) for path in missing_manifests)}")
        if matches:
            version_matches.extend(matches)
        else:
            missing_projects.append(project.name)

    if version_matches and not missing_projects:
        return version_matches, "version-matched merged bins for all firmware projects"

    if should_require_exact_firmware_tag(tag):
        raise RuntimeError(
            f"firmware projects do not all contain merged bins matching tag '{tag}': {', '.join(missing_projects)}"
        )

    merged_bins = sorted(path for project in projects for path in (project / "build" / "release").glob("*_merged.bin") if path.is_file())
    if merged_bins:
        return merged_bins, "available merged bins"

    return [], "no firmware release artifacts found"

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
        source_manifest = manifest_path(source)
        shutil.copy2(source_manifest, firmware_dir / source_manifest.name)
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
    parser.add_argument("--tag", help="Target version; defaults to the pc-app MinVer version")
    parser.add_argument("--repo-root", default=".", help="Repository root")
    parser.add_argument("--skip-firmware-build", action="store_true", help="Package already-built firmware artifacts; intended for the top-level release coordinator")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    tag = args.tag or resolve_pc_app_version(repo_root)
    file_version = numeric_file_version(tag)
    print(f"[version] {tag}")
    pc_app_root = repo_root / "pc-app"
    project = pc_app_root / "BrightnessSensor.ConsoleApp" / "BrightnessSensor.ConsoleApp.csproj"
    publish_dir = pc_app_root / "artifacts" / "single-file" / "win-x64"
    zip_name = f"luma-bloom-pc-app_{tag}_win-x64-portable.zip"
    zip_path = pc_app_root / "artifacts" / "single-file" / zip_name

    if not args.skip_firmware_build:
        build_all_firmware(repo_root, tag)

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
            f"/p:MinVerVersionOverride={tag}",
            f"/p:Version={tag}",
            f"/p:AssemblyVersion={file_version}",
            f"/p:FileVersion={file_version}",
            f"/p:InformationalVersion={tag}",
            f"/p:AssemblyInformationalVersion={tag}",
            "/p:IncludeSourceRevisionInInformationalVersion=false",
            "/p:PublishSingleFile=true",
            "/p:IncludeNativeLibrariesForSelfExtract=true",
            "/p:EnableCompressionInSingleFile=true",
            "/p:DebugType=None",
            "/p:DebugSymbols=false",
        ],
        repo_root,
    )

    copy_firmware_bundle(repo_root, publish_dir, tag)
    copy_esptool(repo_root, publish_dir)
    validate_publish_output(publish_dir, tag)

    zip_dir(publish_dir, zip_path, f"luma-bloom-pc-app_{tag}_win-x64")

    print(f"[ok] publish folder: {publish_dir}")
    print(f"[ok] portable zip: {zip_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
