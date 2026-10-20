@echo off
setlocal
set "NEXUS_APP_DIR=%~dp0artifacts\app-v0.9-gaming-vpn"
if not exist "%NEXUS_APP_DIR%\Nexus.UI.exe" (
  echo A publicacao 0.9 ainda nao esta nesta pasta. Consulta VALIDATION-09.md.
  pause
  exit /b 1
)
start "" /d "%NEXUS_APP_DIR%" "%NEXUS_APP_DIR%\Nexus.UI.exe"
endlocal
