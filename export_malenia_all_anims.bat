@echo off
setlocal

rem Exports all Malenia (c2120) animations.
rem c2120.anibnd.dcx is the base DivBinder; it pulls c2120_div00/c2120_div01
rem when those sibling binders are extracted beside it.

set "EXE=%~dp0artifacts\publish\win-x64-self-contained\er-char-export.exe"
set "GAME_DIR=D:\SteamLibrary\steamapps\common\ELDEN RING\Game"
set "BLENDER=C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
set "WITCHY=D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\WitchyBND-v3.0.0.1-win-x64\WitchyBND.exe"
set "NUXE_RES=D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\Nuxe.1.2.0\Nuxe 1.2.0\res"
set "SOULSTRUCT=D:\RE_EXTRACT\ELDEN_RING_EXTRACT\tools\io_soulstruct-2.6.0"
set "OUT_ROOT=D:\RE_EXTRACT\ELDEN_RING_EXTRACT\exports\malenia-all-anims"

if not exist "%EXE%" (
    echo Missing exporter EXE: "%EXE%"
    exit /b 1
)

echo.
echo === Exporting Malenia all animations from c2120.anibnd.dcx ===
"%EXE%" export ^
  --character c2120 ^
  --animation-binder "c2120.anibnd.dcx" ^
  --game-dir "%GAME_DIR%" ^
  --blender "%BLENDER%" ^
  --witchy "%WITCHY%" ^
  --nuxe-res "%NUXE_RES%" ^
  --soulstruct "%SOULSTRUCT%" ^
  --out "%OUT_ROOT%" ^
  --source-scale 100 ^
  --texture-quality high
if errorlevel 1 exit /b %errorlevel%

echo.
echo DONE: Malenia export written under "%OUT_ROOT%\c2120"
exit /b 0
