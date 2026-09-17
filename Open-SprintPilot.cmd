@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -File "%~dp0scripts\Start-SprintPilot.ps1" %*
if errorlevel 1 (
  echo.
  echo SprintPilot could not complete this step. See the error above.
  pause
  exit /b 1
)
