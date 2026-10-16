@echo off
setlocal
set "NEXUS_APP_DIR=%~dp0artifacts\app-v0.10"
rem Prefer the official installed Store package once version 0.10 or newer is available.
powershell.exe -NoLogo -NoProfile -NonInteractive -Command "$p=Get-AppxPackage -Name 'Zokamg.NEXUSOptimizer' -ErrorAction SilentlyContinue; if($p -and [version]$p.Version -ge [version]'0.10.0.0'){exit 0}; exit 1" >nul 2>&1
if not errorlevel 1 (
  start "" explorer.exe "shell:AppsFolder\Zokamg.NEXUSOptimizer_rvw4exxq8tntt!NexusOptimizer"
  exit /b 0
)
if not exist "%NEXUS_APP_DIR%\Nexus.UI.exe" (
  echo A publicacao 0.10 ainda nao esta nesta pasta. Consulta VPN-IMPLEMENTATION.md.
  pause
  exit /b 1
)
start "" /d "%NEXUS_APP_DIR%" "%NEXUS_APP_DIR%\Nexus.UI.exe"
endlocal
