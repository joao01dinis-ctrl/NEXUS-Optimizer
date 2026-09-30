@echo off
if exist "%~dp0artifacts\app\Nexus.UI.exe" (
    start "NEXUS Optimizer" "%~dp0artifacts\app\Nexus.UI.exe"
) else (
    echo Executavel ainda nao publicado. Execute build.ps1 -Publish.
    pause
)
