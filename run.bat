@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

title TimeTracker Pro

where python >nul 2>&1
if errorlevel 1 (
    echo Python nao encontrado.
    echo Instale Python 3.8 ou superior em https://www.python.org/downloads/
    echo Marque a opcao "Add python.exe to PATH" durante a instalacao.
    pause
    exit /b 1
)

set "VENV_DIR=venv"
set "VENV_PY=%VENV_DIR%\Scripts\python.exe"
set "VENV_PYW=%VENV_DIR%\Scripts\pythonw.exe"

if not exist "%VENV_PY%" (
    echo Criando ambiente virtual...
    python -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo Falha ao criar o ambiente virtual.
        pause
        exit /b 1
    )
)

echo Atualizando pip...
"%VENV_PY%" -m pip install --upgrade pip
if errorlevel 1 (
    echo Falha ao atualizar o pip.
    pause
    exit /b 1
)

echo Instalando dependencias...
"%VENV_PY%" -m pip install -r requirements.txt
if errorlevel 1 (
    echo Falha ao instalar dependencias.
    pause
    exit /b 1
)

echo Iniciando TimeTracker Pro...
if exist "%VENV_PYW%" (
    start "" "%VENV_PYW%" main.py
) else (
    start "" "%VENV_PY%" main.py
)

timeout /t 2 /nobreak >nul
exit /b 0
