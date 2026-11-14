@echo off
powershell.exe -NoProfile -File "%~dp0tools\prepare-pointer-radio-builds.ps1" -InstallAndLaunch
if errorlevel 1 (
    echo Build failed. See the error above. Existing builds may be older.
    pause
    exit /b 1
)
echo Updated Quest app installed. Laptop and Quest apps are ready to test.
pause
