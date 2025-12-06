@echo off
setlocal enabledelayedexpansion

:: Path to IsoMounter.exe
for %%i in ("%cd%\..\..\..\..\plugins\IsoMounter\IsoMounter.exe") do set "IsoMounter_path=%%~fi"

:: Check if the file exists
if not exist "%IsoMounter_path%" (
    exit /b 1
)

:: Mount the ISO
"%IsoMounter_path%" %*

endlocal