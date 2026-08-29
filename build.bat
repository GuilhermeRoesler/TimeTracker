@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK nao encontrado. Instale em https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo == TimeTracker Pro — build Debug ==
echo.
echo Compila a solution e forca copia fresca do wwwroot para bin\
echo (o build incremental do run.bat as vezes nao atualiza CSS/JS).
echo.

rem --no-incremental: evita cache MSBuild pular a copia de Content/wwwroot
dotnet build TimeTracker.sln -c Debug --no-incremental
if errorlevel 1 (
    echo.
    echo Build falhou.
    pause
    exit /b 1
)

echo.
echo Build OK.
echo Output Tracker: src\TimeTracker.Tracker\bin\Debug\net8.0-windows\
echo.
echo Proximo passo: feche o app na bandeja ^(Sair^) e rode run.bat
echo Se o WebView2 ainda mostrar UI antiga, apague a pasta:
echo   %%LocalAppData%%\TimeTracker Pro\WebView2
echo.
pause
exit /b 0
