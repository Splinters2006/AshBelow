@echo off
setlocal
rem Runs the Ash Below updater on Windows. When no Python 3.10+ is installed, a private copy of Python is downloaded
rem once from python.org (checksum-verified) into %LOCALAPPDATA%\AshBelow\python. It needs no admin rights
rem and changes nothing else on the PC.
set "PRIVATE_DIR=%LOCALAPPDATA%\AshBelow\python"
set "PYTHON="
call :try "%PRIVATE_DIR%\python.exe"
if not defined PYTHON call :try py -3
if not defined PYTHON call :try python
if defined PYTHON goto run

echo Python 3.10 or newer was not found. Setting up a private copy for the updater (one time only, about 12 MB)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-python.ps1" -Destination "%PRIVATE_DIR%"
call :try "%PRIVATE_DIR%\python.exe"
if defined PYTHON goto run

echo.
echo Python could not be set up automatically. Check your internet connection and run Update.cmd again,
echo or install Python 3.10 or newer from https://www.python.org/downloads/ and then run this again.
pause
exit /b 1

:run
rem The updater replaces this file while it runs, so everything after it stays on one line that cmd has already read.
rem The game's own update button sets ASHBELOW_NO_PAUSE, so a successful update closes this window by itself.
%PYTHON% "%~dp0update-game.py" --mode release --platform windows --directory "%~dp0." %* && (echo. & (if not defined ASHBELOW_NO_PAUSE pause) & exit /b 0) || (echo. & pause & exit /b 1)

rem Sets PYTHON to the given command if it runs Python 3.10 or newer.
:try
%* -c "import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)" >nul 2>nul
if not errorlevel 1 set PYTHON=%*
exit /b 0
