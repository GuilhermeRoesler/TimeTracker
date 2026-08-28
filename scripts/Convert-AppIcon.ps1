#Requires -Version 5.1
<#
.SYNOPSIS
  Gera assets/app.ico (multi-tamanho) e favicons do dashboard a partir de assets/app-icon.png.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "assets"
$wwwroot = Join-Path $root "src\TimeTracker.Dashboard\wwwroot"
$sourcePng = Join-Path $assets "app-icon.png"

if (-not (Test-Path $sourcePng)) {
    throw "Fonte nao encontrada: $sourcePng"
}

Add-Type -AssemblyName System.Drawing

function Get-ResizedPngBytes([System.Drawing.Image]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

function Save-Png([System.Drawing.Image]$source, [int]$size, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$source = [System.Drawing.Image]::FromFile($sourcePng)
$sizes = @(16, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[object]
foreach ($size in $sizes) {
    $png = Get-ResizedPngBytes $source $size
    $images.Add(@($size, $png)) | Out-Null
}

$icoPath = Join-Path $assets "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$images.Count)
$offset = 6 + (16 * $images.Count)
foreach ($entry in $images) {
    $size = [int]$entry[0]
    $png = [byte[]]$entry[1]
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([int]$png.Length)
    $bw.Write([int]$offset)
    $offset += $png.Length
}
foreach ($entry in $images) {
    $bw.Write([byte[]]$entry[1])
}
$bw.Close()
$fs.Close()

New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item $icoPath (Join-Path $wwwroot "favicon.ico") -Force
Save-Png $source 32 (Join-Path $wwwroot "favicon-32.png")
Save-Png $source 192 (Join-Path $wwwroot "icon-192.png")
Copy-Item (Join-Path $wwwroot "favicon-32.png") (Join-Path $assets "favicon-32.png") -Force
$source.Dispose()

Write-Host ">> Gerado: $icoPath"
Write-Host ">> Favicons atualizados em $wwwroot"
