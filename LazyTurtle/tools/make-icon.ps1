<#
.SYNOPSIS
    Draws the turtle application icon and writes src\LazyTurtle\Assets\app.ico
    (256/64/48/32/16 px, PNG-compressed entries - supported since Windows Vista).
    Only needed when you want to change the icon; the .ico is checked in.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\LazyTurtle\Assets\app.ico'

function New-TurtlePng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0

    $skin = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, 205, 90))
    $dark = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(35, 80, 25)), ([Math]::Max(1, 8 * $s))
    # legs
    foreach ($p in @(@(40, 70), @(176, 70), @(40, 160), @(176, 160))) {
        $g.FillEllipse($skin, $p[0] * $s, $p[1] * $s, 44 * $s, 40 * $s)
        $g.DrawEllipse($dark, $p[0] * $s, $p[1] * $s, 44 * $s, 40 * $s)
    }
    # head
    $g.FillEllipse($skin, 92 * $s, 8 * $s, 72 * $s, 70 * $s)
    $g.DrawEllipse($dark, 92 * $s, 8 * $s, 72 * $s, 70 * $s)
    $g.FillEllipse([System.Drawing.Brushes]::White, 104 * $s, 24 * $s, 20 * $s, 22 * $s)
    $g.FillEllipse([System.Drawing.Brushes]::White, 132 * $s, 24 * $s, 20 * $s, 22 * $s)
    $g.FillEllipse([System.Drawing.Brushes]::Black, 110 * $s, 30 * $s, 10 * $s, 12 * $s)
    $g.FillEllipse([System.Drawing.Brushes]::Black, 138 * $s, 30 * $s, 10 * $s, 12 * $s)
    # shell
    $rect = New-Object System.Drawing.RectangleF (48 * $s), (52 * $s), (160 * $s), (180 * $s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(140, 190, 60)), ([System.Drawing.Color]::FromArgb(45, 105, 30)), 60
    $g.FillEllipse($grad, $rect)
    $gold = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(240, 205, 90)), ([Math]::Max(1, 9 * $s))
    $g.DrawEllipse($gold, $rect)
    $pts = for ($i = 0; $i -lt 6; $i++) {
        $a = $i * [Math]::PI / 3 + [Math]::PI / 6
        New-Object System.Drawing.PointF ((128 + [Math]::Cos($a) * 42) * $s), ((142 + [Math]::Sin($a) * 42) * $s)
    }
    $g.DrawPolygon($gold, [System.Drawing.PointF[]]$pts)
    for ($i = 0; $i -lt 6; $i++) {
        $a = $i * [Math]::PI / 3 + [Math]::PI / 6
        $g.DrawLine($gold, (128 + [Math]::Cos($a) * 42) * $s, (142 + [Math]::Sin($a) * 42) * $s, (128 + [Math]::Cos($a) * 78) * $s, (142 + [Math]::Sin($a) * 86) * $s)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 64, 48, 32, 16
$images = foreach ($sz in $sizes) { , (New-TurtlePng $sz) }

$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Write-Host "Icon written to $out"
