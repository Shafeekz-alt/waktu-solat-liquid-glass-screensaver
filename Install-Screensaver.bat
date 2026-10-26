@echo off
title Pasang Screensaver Waktu Solat Malaysia (Windows 11)
set "SCR_PATH=%~dp0WaktuSolatLiquidGlass.scr"

echo Memasang WaktuSolatLiquidGlass.scr sebagai Screensaver Windows 11...
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%SCR_PATH%" /f
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d "1" /f
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut /t REG_SZ /d "300" /f

echo.
echo Berjaya dipasang! Membuka tetapan Screen Saver Windows 11...
start "" control desk.cpl,,@screensaver
