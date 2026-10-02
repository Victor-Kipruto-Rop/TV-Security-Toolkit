@echo off
setlocal

set "APP=%~dp0TV-Security-Toolkit.exe"
if not exist "%APP%" (
    echo TV-Security-Toolkit.exe was not found beside this launcher.
    echo Extract or copy the complete USB package, then try again.
    pause
    exit /b 1
)

start "" /D "%~dp0" "%APP%"
