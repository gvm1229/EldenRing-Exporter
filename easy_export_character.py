r"""
Easy Elden Ring character export runner.

Normal use:
  1. Edit the variables in USER SETTINGS.
  2. Run this file with Python:

       python easy_export_character.py

This is a thin launcher around er-char-export.exe. The real extraction/export
logic stays in the tested EXE; this file exists so common edits are simple.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path


# =========================
# USER SETTINGS
# =========================

# Character ID examples:
#   c3181 = Red Wolf of Radagon
#   c2120 = Malenia
CHARACTER_CODE = "c3181"

# Export format: "fbx" is best for Unreal skeletal animations; "glb" is useful
# for Blender/web viewers but Unreal's GLB skeletal animation import is limited.
EXPORT_FORMAT = "fbx"

# Export one animation for fast testing, or all animations.
EXPORT_ALL_ANIMATIONS = False
ANIMATION_NAME = "a000_001020"
# EXPORT_ALL_ANIMATIONS = True  # all animations

# Optional binder override. Leave blank for the default/base binder.
# Malenia all-animation export should use: "c2120.anibnd.dcx"
ANIMATION_BINDER = ""

# Optional first-N animation limit. Set to 0 for no limit.
LIMIT_ANIMS = 0

# Scale behavior:
#   100 = current UE-size default used by the Red Wolf golden FBX.
#   1   = 1/100 scale test mode used for smaller GLB experiments.
SOURCE_SCALE = 100

# Texture quality: "high", "low", or "none".
TEXTURE_QUALITY = "high"

# If True, do not generate the FBX Unreal Python import helper.
SKIP_UE_SCRIPT = False


# =========================
# PATH SETTINGS
# =========================

GAME_DIR = r"D:\SteamLibrary\steamapps\common\ELDEN RING\Game"
BLENDER_EXE = r"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"

# Output root. The tool writes to: OUTPUT_ROOT / CHARACTER_CODE / ...
OUTPUT_ROOT = r"D:\RE_EXTRACT\ELDEN_RING_EXTRACT\exports\easy-script"

# These defaults work from either a source checkout or the packaged zip layout.
REPO_OR_PACKAGE_ROOT = Path(__file__).resolve().parent
EXPORTER_EXE = REPO_OR_PACKAGE_ROOT / "artifacts" / "publish" / "win-x64-self-contained" / "er-char-export.exe"
if not EXPORTER_EXE.is_file():
    EXPORTER_EXE = REPO_OR_PACKAGE_ROOT / "er-char-export.exe"

WITCHY_EXE = REPO_OR_PACKAGE_ROOT / "external" / "WitchyBND-release" / "WitchyBND-v3.0.0.1-win-x64" / "WitchyBND.exe"
NUXE_RES_DIR = REPO_OR_PACKAGE_ROOT / "external" / "Nuxe" / "dist" / "res"
SOULSTRUCT_ADDON_ROOT = REPO_OR_PACKAGE_ROOT / "external" / "soulstruct-blender-release" / "io_soulstruct-2.6.0"


def require_file(label: str, path: str | Path) -> None:
    path = Path(path)
    if not path.is_file():
        raise FileNotFoundError(f"{label} does not exist: {path}")


def require_dir(label: str, path: str | Path) -> None:
    path = Path(path)
    if not path.is_dir():
        raise FileNotFoundError(f"{label} does not exist: {path}")


def build_command() -> list[str]:
    command = [
        str(EXPORTER_EXE),
        "export",
        "--character", CHARACTER_CODE,
        "--format", EXPORT_FORMAT,
        "--game-dir", GAME_DIR,
        "--blender", BLENDER_EXE,
        "--witchy", str(WITCHY_EXE),
        "--nuxe-res", str(NUXE_RES_DIR),
        "--soulstruct", str(SOULSTRUCT_ADDON_ROOT),
        "--out", OUTPUT_ROOT,
        "--source-scale", str(SOURCE_SCALE),
        "--texture-quality", TEXTURE_QUALITY,
    ]

    if not EXPORT_ALL_ANIMATIONS and ANIMATION_NAME.strip():
        command.extend(["--anim", ANIMATION_NAME.strip()])
    if ANIMATION_BINDER.strip():
        command.extend(["--animation-binder", ANIMATION_BINDER.strip()])
    if LIMIT_ANIMS > 0:
        command.extend(["--limit-anims", str(LIMIT_ANIMS)])
    if SKIP_UE_SCRIPT:
        command.append("--skip-ue-script")

    return command


def quote_command(command: list[str]) -> str:
    return " ".join(f'"{part}"' if " " in part else part for part in command)


def main() -> int:
    require_file("Exporter EXE", EXPORTER_EXE)
    require_file("Blender EXE", BLENDER_EXE)
    require_file("WitchyBND EXE", WITCHY_EXE)
    require_dir("Game directory", GAME_DIR)
    require_dir("Nuxe resource directory", NUXE_RES_DIR)
    require_dir("Soulstruct add-on root", SOULSTRUCT_ADDON_ROOT)

    command = build_command()
    print("Running:")
    print(quote_command(command))
    print()

    result = subprocess.run(command, cwd=str(REPO_OR_PACKAGE_ROOT))
    if result.returncode != 0:
        return result.returncode

    character_out = Path(OUTPUT_ROOT) / CHARACTER_CODE
    exported_asset = character_out / "exports" / f"{CHARACTER_CODE}_ue5.{EXPORT_FORMAT}"
    print()
    print("Done.")
    print(f"Asset: {exported_asset}")
    print(f"Log: {character_out / 'exports' / f'{CHARACTER_CODE}_ue5.blender.log'}")
    print(f"Manifest: {character_out / 'manifest.json'}")
    if EXPORT_FORMAT == "fbx" and not SKIP_UE_SCRIPT:
        print(f"Unreal script: {character_out / 'ue' / f'import_{CHARACTER_CODE}_to_unreal.py'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise SystemExit(1)
