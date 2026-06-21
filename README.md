# Elden Ring Character Exporter

Windows CLI tool for exporting Elden Ring character FLVER/HKX assets to UE-ready FBX.

V1 uses installed external tools:

- Blender 4.5+
- Soulstruct Blender add-on
- WitchyBND
- Nuxe/Coremats resources for Elden Ring archive keys and hash dictionary

## Example

```powershell
dotnet run --project .\src\ErCharExport -- export `
  --character c3181 `
  --game-dir "D:\SteamLibrary\steamapps\common\ELDEN RING\Game" `
  --blender "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --witchy "D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\WitchyBND-v3.0.0.1-win-x64\WitchyBND.exe" `
  --nuxe-res "D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\Nuxe.1.2.0\Nuxe 1.2.0\res" `
  --soulstruct "D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\io_soulstruct-2.6.0" `
  --out "D:\RE_EXTRACT\ELDEN_RING_EXTRACT\exports"
```

Outputs are written under `<out>\<character>\`.

## Commands

- `list`: list discovered character IDs from `Data3.txt`.
- `inspect --character c3181`: show available binders for one character.
- `export --character c3181`: extract, unpack, export FBX, copy DDS textures, and generate Unreal Python import script.

## Notes

The FBX export uses the Red Wolf-verified source-space scale fix:

- FLVER translation/position data scaled before Blender import.
- HKX skeleton and animation translations scaled before Blender action creation.
- `FBX_SCALE_UNITS`, scene scale `1.0`, action export, no NLA strips.

