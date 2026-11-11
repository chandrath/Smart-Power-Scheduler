@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK is not found in PATH.
    echo Please install .NET 10 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: Build if needed (quiet Release build)
dotnet build "%~dp0src\Smart-Sleep-Scheduler-Win\SmartSleepScheduler.csproj" -c Release --nologo -v q
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. Check the error output above.
    pause
    exit /b 1
)

:: Launch detached GUI process and close terminal window immediately
start "" "%~dp0src\Smart-Sleep-Scheduler-Win\bin\Release\net10.0-windows\SmartSleepScheduler.exe"
exit /b 0
