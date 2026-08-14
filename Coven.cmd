@echo off
rem Launches the Coven desktop app.
rem
rem Every path is derived from this file's own location (%~dp0), so the repository can live
rem anywhere and be cloned by anyone. A build already present is used as-is; only a clone
rem that has never been built pays for a compile.
rem
rem Keep this file CRLF and ASCII: cmd.exe drops the first character of every LF-only line.

setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\apps\Coven.Ui.Desktop\Coven.Ui.Desktop.csproj"
set "RELEASE=%ROOT%src\apps\Coven.Ui.Desktop\bin\Release\net10.0\Coven.Ui.Desktop.exe"
set "DEBUG=%ROOT%src\apps\Coven.Ui.Desktop\bin\Debug\net10.0\Coven.Ui.Desktop.exe"
set "EXE="

rem Release first, since it is the better thing to run when both are lying around.
call :pick "%RELEASE%"
call :pick "%DEBUG%"

if not defined EXE (
    echo Coven has not been built yet. Building Release - the first build takes a few minutes.
    echo.
    dotnet build "%PROJECT%" -c Release --nologo
    if errorlevel 1 goto :failed
    call :pick "%RELEASE%"
)

if not defined EXE goto :failed

rem start returns immediately and detaches, so this console closes rather than lingering
rem behind the window for as long as the app runs.
start "" "%EXE%"
exit /b 0

:pick
if defined EXE exit /b 0
if exist "%~1" set "EXE=%~1"
exit /b 0

:failed
echo.
echo Could not start Coven.
echo.
echo This needs the .NET 10 SDK on PATH. Check with "dotnet --version", then build manually:
echo     dotnet build "%PROJECT%" -c Release
echo.
rem Without this the window would vanish before anyone double-clicking could read why.
pause
exit /b 1
