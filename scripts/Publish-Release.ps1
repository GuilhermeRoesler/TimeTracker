#Requires -Version 5.1
<#
.SYNOPSIS
  Publica TimeTracker framework-dependent (win-x64) e opcionalmente gera o Setup.exe (Inno Setup).
#>
param(
    [string]$Version = "0.0.0-dev",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$publishDir = Join-Path $root "artifacts\publish"
$installerOut = Join-Path $root "artifacts\installer"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerOut | Out-Null

Write-Host ">> Publicando Tracker (framework-dependent)..."
dotnet publish "src\TimeTracker.Tracker\TimeTracker.Tracker.csproj" `
    -c Release -r win-x64 --self-contained false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ">> Publicando Dashboard (framework-dependent)..."
dotnet publish "src\TimeTracker.Dashboard\TimeTracker.Dashboard.csproj" `
    -c Release -r win-x64 --self-contained false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Remover arquivos de desenvolvimento / símbolos desnecessários
Get-ChildItem $publishDir -Include "*.pdb","*.xml" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
$devSettings = Join-Path $publishDir "appsettings.Development.json"
if (Test-Path $devSettings) { Remove-Item $devSettings -Force }
# WebView2 WPF não é usado (só WinForms)
$wpf = Join-Path $publishDir "Microsoft.Web.WebView2.Wpf.dll"
if (Test-Path $wpf) { Remove-Item $wpf -Force }

$sizeMb = [math]::Round(((Get-ChildItem $publishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 2)
Write-Host ">> Publish OK: $publishDir ($sizeMb MB)"

if ($SkipInstaller) {
    Write-Host ">> Instalador ignorado (-SkipInstaller)."
    exit 0
}

$iscc = @(
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup 6 (ISCC.exe) nao encontrado. Instale em https://jrsoftware.org/isinfo.php"
    Write-Host "Publish concluido sem Setup.exe."
    exit 0
}

Write-Host ">> Compilando instalador com $iscc ..."
& $iscc "/DMyAppVersion=$Version" (Join-Path $root "installer\TimeTracker.iss")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem $installerOut | Format-Table Name, Length -AutoSize
Write-Host ">> Concluido."
