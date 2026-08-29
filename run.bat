@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK nao encontrado. Instale em https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

rem --no-hot-reload: evita o host do `dotnet run` ficar preso apos o WinExe encerrar pela bandeja
dotnet run --project src\TimeTracker.Tracker\TimeTracker.Tracker.csproj --no-hot-reload
