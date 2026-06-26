@echo off
setlocal

rem Edit easy_export_character.py first, then run this batch file.
set "SCRIPT_DIR=%~dp0"
set "PYTHON_EXE=python"

"%PYTHON_EXE%" "%SCRIPT_DIR%easy_export_character.py"
exit /b %errorlevel%
