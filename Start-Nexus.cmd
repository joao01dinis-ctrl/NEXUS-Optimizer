@echo off
if exist "%~dp0artifacts\app-v0.5-final\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app-v0.5-final\Nexus.UI.exe"
) else if exist "%~dp0artifacts\app-v0.5\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app-v0.5\Nexus.UI.exe"
) else if exist "%~dp0artifacts\app-v0.4\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app-v0.4\Nexus.UI.exe"
) else if exist "%~dp0artifacts\app-v0.3\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app-v0.3\Nexus.UI.exe"
) else if exist "%~dp0artifacts\app-v0.2\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app-v0.2\Nexus.UI.exe"
) else if exist "%~dp0artifacts\app\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app\Nexus.UI.exe"
) else (
    echo Executavel ainda nao publicado. Execute build.ps1 -Publish.
    pause
)
