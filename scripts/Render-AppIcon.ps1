#Requires -Version 5.1
<#
.SYNOPSIS
  Rasteriza assets/app-icon.png (1024²) a partir do desenho canônico do produto
  e regenera .ico / favicons via Convert-AppIcon.ps1.

  O master vetorial fica em assets/app-icon.svg (referência; este script espelha o desenho em GDI+).
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "assets"
$outPng = Join-Path $assets "app-icon.png"
$size = 1024
$cornerRadius = 220.0

Add-Type -AssemblyName System.Drawing

function New-Color([string]$hex) {
    $h = $hex.TrimStart('#')
    $r = [Convert]::ToInt32($h.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($h.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($h.Substring(4, 2), 16)
    return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

function Add-RoundRect(
    [System.Drawing.Graphics]$gr,
    [System.Drawing.Brush]$br,
    [float]$x,
    [float]$y,
    [float]$w,
    [float]$h,
    [float]$r
) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $gr.FillPath($br, $path)
    $path.Dispose()
}

# --accent, arco em --surface-2 (creme do painel), ponteiros --warning
$accent = New-Color '#0e7490'
$ring = New-Color '#f3f6fa'
$face = [System.Drawing.Color]::White
$hands = New-Color '#b45309'

$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

$g.Clear([System.Drawing.Color]::Transparent)
$bgBrush = New-Object System.Drawing.SolidBrush $accent
Add-RoundRect $g $bgBrush 0.0 0.0 ([float]$size) ([float]$size) $cornerRadius
$bgBrush.Dispose()

$cx = 512.0
$cy = 500.0

$ringWidth = 56.0
$ringRadius = 318.0
$ringRect = New-Object System.Drawing.RectangleF (
    [float]($cx - $ringRadius),
    [float]($cy - $ringRadius),
    [float](2 * $ringRadius),
    [float](2 * $ringRadius)
)
$ringPen = New-Object System.Drawing.Pen $ring, $ringWidth
$ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($ringPen, $ringRect, 200.0, 220.0)
$ringPen.Dispose()

$faceR = 248.0
$faceRect = New-Object System.Drawing.RectangleF (
    [float]($cx - $faceR),
    [float]($cy - $faceR),
    [float](2 * $faceR),
    [float](2 * $faceR)
)
$faceBrush = New-Object System.Drawing.SolidBrush $face
$g.FillEllipse($faceBrush, $faceRect)
$faceBrush.Dispose()

$tickBrush = New-Object System.Drawing.SolidBrush $accent
$tickW = 32.0
$tickH = 56.0
$rx = 10.0
Add-RoundRect $g $tickBrush ([float]($cx - $tickW / 2)) ([float]($cy - $faceR + 44)) $tickW $tickH $rx
Add-RoundRect $g $tickBrush ([float]($cx - $tickW / 2)) ([float]($cy + $faceR - 44 - $tickH)) $tickW $tickH $rx
Add-RoundRect $g $tickBrush ([float]($cx - $faceR + 44)) ([float]($cy - $tickW / 2)) $tickH $tickW $rx
Add-RoundRect $g $tickBrush ([float]($cx + $faceR - 44 - $tickH)) ([float]($cy - $tickW / 2)) $tickH $tickW $rx
$tickBrush.Dispose()

$handPenH = New-Object System.Drawing.Pen $hands, 44.0
$handPenH.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$handPenH.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$handPenM = New-Object System.Drawing.Pen $hands, 36.0
$handPenM.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$handPenM.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($handPenH, [float]$cx, [float]$cy, [float]($cx - 106), [float]($cy - 70))
$g.DrawLine($handPenM, [float]$cx, [float]$cy, [float]($cx + 106), [float]($cy - 70))
$handPenH.Dispose()
$handPenM.Dispose()

$dotBrush = New-Object System.Drawing.SolidBrush $hands
$dotR = 28.0
$g.FillEllipse($dotBrush, [float]($cx - $dotR), [float]($cy - $dotR), [float](2 * $dotR), [float](2 * $dotR))
$dotBrush.Dispose()

$g.Dispose()

$bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host ">> Rasterizado: $outPng"

& (Join-Path $PSScriptRoot "Convert-AppIcon.ps1")
