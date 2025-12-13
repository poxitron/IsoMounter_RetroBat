@echo off
setlocal enabledelayedexpansion

:: Relative path to IsoMounter.exe
for %%i in ("%cd%\..\..\..\..\plugins\IsoMounter\IsoMounter.exe") do set "IsoMounter_path=%%~fi"

:: Check if the file exists
if not exist "%IsoMounter_path%" (
    echo Error: IsoMounter.exe not found
    echo Path should be: %IsoMounter_path%
    pause
    exit /b 1
)

:: Mount the ISO
"%IsoMounter_path%" %*

endlocal