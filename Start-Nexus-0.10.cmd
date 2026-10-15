@echo off
setlocal
set "NEXUS_APP_DIR=%~dp0artifacts\app-v0.10-protection"
if not exist "%NEXUS_APP_DIR%\Nexus.UI.exe" (
  echo A publicacao 0.10 ainda nao esta nesta pasta. Consulta PROTECTION.md.
  pause
  exit /b 1
)
start "" /d "%NEXUS_APP_DIR%" "%NEXUS_APP_DIR%\Nexus.UI.exe"
endlocal
