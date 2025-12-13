@echo off
setlocal enabledelayedexpansion

:: Relative path to IsoMounter.exe (same structure as mount_iso.bat)
for %%i in ("%cd%\..\..\..\..\plugins\IsoMounter\IsoMounter.exe") do set "IsoMounter_path=%%~fi"

:: Check if the file exists
if not exist "%IsoMounter_path%" (
    echo Error: IsoMounter.exe not found
    echo Path should be: %IsoMounter_path%
    pause
    exit /b 1
)

:: Unmount the ISO
"%IsoMounter_path%" --unmount

exit /b 0
