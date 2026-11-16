<#
.SYNOPSIS
    Single Source of Truth (SSOT) Icon & Asset Generation Pipeline.
    Generates all platform icons from master-icon.png:
      1. Assets/app.png (256x256 UI display image)
      2. Assets/app.ico (Multi-size Windows icon: 256, 128, 64, 48, 32, 16)
      3. StreamDeck Plugin: pluginIcon.png, pluginIcon@2x.png, actionIcon.png, actionIcon@2x.png

.DESCRIPTION
    Whenever a new logo is introduced, replace 'master-icon.png' (minimum 512x512)
    and run this script. All application, installer, and Stream Deck assets update automatically.
#>
[CmdletBinding()]
param(
    [string]$MasterPath = "$PSScriptRoot\master-icon.png",
    [string]$AssetsDir = "$PSScriptRoot",
    [string]$StreamDeckDir = "$PSScriptRoot\..\StreamDeck.Plugin\com.appsphinx.smartpowerscheduler.sdPlugin\images"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $MasterPath)) {
    throw "Master icon file not found at: $MasterPath"
}

Add-Type -AssemblyName System.Drawing

function Resize-To-PngBytes($src, $w, $h) {
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, $destRect, 0, 0, $src.Width, $src.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
    return @{ Width = $w; Height = $h; Bytes = $bytes }
}

Write-Host "Updating icon pipeline from SSOT master: $(Split-Path $MasterPath -Leaf)..." -ForegroundColor Cyan

$source = [System.Drawing.Image]::FromFile($MasterPath)

try {
    # 1. Generate app.png (256x256)
    $appPng = Resize-To-PngBytes $source 256 256
    [System.IO.File]::WriteAllBytes((Join-Path $AssetsDir "app.png"), $appPng.Bytes)
    Write-Host "  -> Generated Assets/app.png (256x256)"

    # 2. Generate multi-resolution app.ico (256, 128, 64, 48, 32, 16)
    $sizes = @(256, 128, 64, 48, 32, 16)
    $icoItems = @()
    foreach ($sz in $sizes) {
        $icoItems += (Resize-To-PngBytes $source $sz $sz)
    }

    $icoPath = Join-Path $AssetsDir "app.ico"
    $fs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$icoItems.Count)

    $offset = 6 + (16 * $icoItems.Count)
    foreach ($it in $icoItems) {
        $wByte = if ($it.Width -ge 256) { [byte]0 } else { [byte]$it.Width }
        $hByte = if ($it.Height -ge 256) { [byte]0 } else { [byte]$it.Height }
        $bw.Write($wByte)
        $bw.Write($hByte)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$it.Bytes.Length)
        $bw.Write([uint32]$offset)
        $offset += $it.Bytes.Length
    }

    foreach ($it in $icoItems) {
        $bw.Write($it.Bytes)
    }
    $bw.Flush()
    $bw.Close()
    $fs.Close()
    Write-Host "  -> Generated Assets/app.ico (256, 128, 64, 48, 32, 16 px)"

    # 3. Stream Deck Plugin Icons
    if (Test-Path $StreamDeckDir) {
        # Category / Store icon (72x72 and 144x144)
        $pluginIcon72 = Resize-To-PngBytes $source 72 72
        $pluginIcon144 = Resize-To-PngBytes $source 144 144
        [System.IO.File]::WriteAllBytes((Join-Path $StreamDeckDir "pluginIcon.png"), $pluginIcon72.Bytes)
        [System.IO.File]::WriteAllBytes((Join-Path $StreamDeckDir "pluginIcon@2x.png"), $pluginIcon144.Bytes)
        Write-Host "  -> Generated StreamDeck pluginIcon.png (72x72) & @2x (144x144)"

        # Action list & state icon (72x72 and 144x144)
        [System.IO.File]::WriteAllBytes((Join-Path $StreamDeckDir "actionIcon.png"), $pluginIcon72.Bytes)
        [System.IO.File]::WriteAllBytes((Join-Path $StreamDeckDir "actionIcon@2x.png"), $pluginIcon144.Bytes)
        Write-Host "  -> Generated StreamDeck actionIcon.png (72x72) & @2x (144x144)"
    }
}
finally {
    $source.Dispose()
}

Write-Host "Icon pipeline complete. All assets synchronized with SSOT master." -ForegroundColor Green
