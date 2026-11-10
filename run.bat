@echo off
title Smart Sleep Scheduler - Dev Runner
setlocal

cd /d "%~dp0"

echo ========================================================
echo   Smart Sleep Scheduler - Launching WPF App (.NET 10)
echo ========================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK is not found in PATH.
    echo Please install .NET 10 SDK: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

echo Building and starting Smart Sleep Scheduler...
dotnet run --project "%~dp0src\Smart-Sleep-Scheduler-Win\SmartSleepScheduler.csproj"

if errorlevel 1 (
    echo.
    echo [ERROR] Application failed to launch or exited with error.
    echo.
    pause
    exit /b 1
)

endlocal
