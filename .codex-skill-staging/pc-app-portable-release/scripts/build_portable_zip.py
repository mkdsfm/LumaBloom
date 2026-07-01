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
            "/p:PublishSingleFile=true",
            "/p:IncludeNativeLibrariesForSelfExtract=true",
            "/p:EnableCompressionInSingleFile=true",
            "/p:DebugType=None",
            "/p:DebugSymbols=false",
        ],
        repo_root,
    )

    zip_dir(publish_dir, zip_path, "win-x64")

    print(f"[ok] publish folder: {publish_dir}")
    print(f"[ok] portable zip: {zip_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
