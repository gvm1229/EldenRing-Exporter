# Elden Ring Character Exporter

Windows CLI tool for exporting Elden Ring character FLVER/HKX assets to UE-ready FBX or Blender GLB.

The current pipeline was validated on Red Wolf of Radagon (`c3181`) and Malenia (`c2120`). It uses the Red Wolf scale fix:

- FLVER translation/position data is scaled before Blender import.
- HKX skeleton and animation translations are scaled before Blender action creation.
- FBX export uses `FBX_SCALE_UNITS`, scene scale `1.0`, action export, and no NLA strips.
- GLB export uses the same imported/scaled scene and Blender's glTF `ACTIONS` animation export mode.

## Dependency Layout

This repo uses git submodules as upstream waypoints, similar to projects that keep external repos visible in the checkout without taking ownership of them.

| Path | Upstream | Purpose |
|---|---|---|
| `external/soulstruct-blender` | [Grimrukh/soulstruct-blender](https://github.com/Grimrukh/soulstruct-blender) | Source waypoint for the Blender add-on. |
| `external/WitchyBND` | [ividyon/WitchyBND](https://github.com/ividyon/WitchyBND) | Source waypoint for FromSoftware binder unpacking. |
| `external/Nuxe` | [JKAnderson/Nuxe](https://github.com/JKAnderson/Nuxe) | Source waypoint for archive resources and hash/key data. |

`scripts/setup_dependencies.ps1` initializes those submodules and also downloads release artifacts that are more appropriate for automation:

- `external/soulstruct-blender-release/io_soulstruct-2.6.0`
- `external/WitchyBND-release/WitchyBND-v3.0.0.1-win-x64`
- `external/Nuxe/dist/res`

The release folders are ignored by git. They are reproducible local setup output, not this repo's source.

## Quick Start

Clone with submodules:

```powershell
git clone --recurse-submodules https://github.com/gvm1229/EldenRing-Exporter.git
cd EldenRing-Exporter
```

If you already cloned without submodules:

```powershell
git submodule update --init --recursive
```

Prepare local external tools:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup_dependencies.ps1
```

Build the exporter:

```powershell
dotnet publish .\src\ErCharExport\ErCharExport.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64-self-contained
```

## Easiest Workflow

For normal use, edit the variables at the top of:

```powershell
.\easy_export_character.py
```

Then run:

```powershell
python .\easy_export_character.py
```

or:

```powershell
.\easy_export_character.bat
```

The important editable variables are:

- `CHARACTER_CODE`: target character, for example `c3181` or `c2120`.
- `EXPORT_FORMAT`: `fbx` or `glb`; use `fbx` for Unreal skeletal animation imports.
- `EXPORT_ALL_ANIMATIONS`: `False` for one animation, `True` for every animation in the selected binder.
- `ANIMATION_NAME`: single-animation target, for example `a000_001020`.
- `ANIMATION_BINDER`: optional binder override, for example `c2120.anibnd.dcx` for Malenia.
- `SOURCE_SCALE`: `100` for the current UE-sized Red Wolf pipeline, `1` for 1/100 scale tests.
- `GAME_DIR`, `BLENDER_EXE`, and `OUTPUT_ROOT`: machine-specific paths.

The Python script is only a launcher. It calls the self-contained `er-char-export.exe`, so the extraction behavior is identical to the CLI path below.

## Example Export

```powershell
.\artifacts\publish\win-x64-self-contained\er-char-export.exe export `
  --character c3181 `
  --game-dir "D:\SteamLibrary\steamapps\common\ELDEN RING\Game" `
  --blender "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --out "D:\RE_EXTRACT\ELDEN_RING_EXTRACT\exports"
```

When run from inside the repo, these options default to repo-local dependency paths:

- `--witchy external\WitchyBND-release\WitchyBND-v3.0.0.1-win-x64\WitchyBND.exe`
- `--nuxe-res external\Nuxe\dist\res`
- `--soulstruct external\soulstruct-blender-release\io_soulstruct-2.6.0`

You still need to provide machine-specific paths for:

- Elden Ring install directory
- Blender executable
- output directory if you do not want `.\exports`

## Malenia Batch

The repo includes:

```powershell
.\export_malenia_all_anims.bat
```

It exports Malenia (`c2120`) through the base `c2120.anibnd.dcx`. That base binder resolves `c2120_div00.anibnd.dcx` and `c2120_div01.anibnd.dcx` when those sibling binders are extracted beside it, producing the full resolved animation set.

## Commands

- `list`: list discovered character IDs from `Data3.txt`.
- `inspect --character c3181`: show available binders for one character.
- `export --character c3181`: extract, unpack, export FBX, copy DDS textures, and generate an Unreal Python import script.

Useful export flags:

- `--anim a000_001020`: export one animation for quick testing.
- `--limit-anims 2`: export the first N animations.
- `--format fbx|glb`
- `--texture-quality high|low|none`
- `--animation-binder c2120.anibnd.dcx`
- `--source-scale 100`
- `--skip-ue-script`

## Outputs

Exports are written under `<out>\<character>\`:

- `exports\<character>_ue5.fbx` or `exports\<character>_ue5.glb`
- `exports\<character>_ue5.blender.log`
- `textures\high\*.dds` or `textures\low\*.dds`
- `ue\import_<character>_to_unreal.py`
- `manifest.json`
