@echo off
rem Install the launcher for the CURRENT user:
rem   Desktop  : click to start the server
rem   Startup  : start the server automatically at logon
rem Run this from the desktop user's own session (double click), not as SYSTEM.
setlocal
set "SRC=%~dp0StartServer.vbs"
set "DST1=%USERPROFILE%\Desktop\StartNganServer.vbs"
set "DST2=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\StartNganServer.vbs"

if not exist "%SRC%" (
	echo StartServer.vbs not found next to this script: %SRC%
	pause
	exit /b 1
)

copy /y "%SRC%" "%DST1%" >nul
if errorlevel 1 (
	echo Failed to write %DST1%
	pause
	exit /b 1
)

if not exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup" (
	mkdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
)
copy /y "%SRC%" "%DST2%" >nul
if errorlevel 1 (
	echo Failed to write %DST2%
	pause
	exit /b 1
)

echo Installed.
echo   Desktop: %DST1%
echo   Startup: %DST2%
pause
