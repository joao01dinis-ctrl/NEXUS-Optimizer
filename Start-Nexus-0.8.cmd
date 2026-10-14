@echo off
setlocal
if not exist "%~dp0artifacts\app-v0.8-redesign\Nexus.UI.exe" (
  echo A publicacao 0.8 ainda nao esta nesta pasta. Consulta VALIDATION-08.md.
  pause
  exit /b 1
)
start "" "%~dp0artifacts\app-v0.8-redesign\Nexus.UI.exe"
