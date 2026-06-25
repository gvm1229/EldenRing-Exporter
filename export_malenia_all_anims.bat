@echo off
setlocal

rem Exports all known Malenia (c2120) animation binders.
rem This creates three packages because c2120 has three ANIBND files:
rem   c2120.anibnd.dcx
rem   c2120_div00.anibnd.dcx
rem   c2120_div01.anibnd.dcx

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

call :export_binder base c2120.anibnd.dcx || exit /b %errorlevel%
call :export_binder div00 c2120_div00.anibnd.dcx || exit /b %errorlevel%
call :export_binder div01 c2120_div01.anibnd.dcx || exit /b %errorlevel%

echo.
echo DONE: Malenia exports written under "%OUT_ROOT%"
exit /b 0

:export_binder
set "LABEL=%~1"
set "BINDER=%~2"
echo.
echo === Exporting Malenia %LABEL% animations from %BINDER% ===
"%EXE%" export ^
  --character c2120 ^
  --animation-binder "%BINDER%" ^
  --game-dir "%GAME_DIR%" ^
  --blender "%BLENDER%" ^
  --witchy "%WITCHY%" ^
  --nuxe-res "%NUXE_RES%" ^
  --soulstruct "%SOULSTRUCT%" ^
  --out "%OUT_ROOT%\%LABEL%" ^
  --source-scale 100 ^
  --texture-quality high
exit /b %errorlevel%

