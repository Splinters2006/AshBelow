@echo off
setlocal
where py >nul 2>nul
if not errorlevel 1 (
    py -3 "%~dp0update-game.py" --mode release --directory "%~dp0." %*
) else (
    python "%~dp0update-game.py" --mode release --directory "%~dp0." %*
)
echo.
echo If Python was not found, install Python 3.10 or newer, then run this again.
pause
