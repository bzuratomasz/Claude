<#
.SYNOPSIS
    Draws the turtle application icon and writes src\LazyTurtle\Assets\app.ico
    (256/64/48/32/16 px, PNG-compressed entries - supported since Windows Vista).
    Only needed when you want to change the icon; the .ico is checked in.

.DESCRIPTION
    A cute cartoon turtle (the game's mascot) with a yellow school backpack on its shell:
    big shiny eyes, a smile, rosy cheeks and a bold dark outline. Every size is drawn directly
    (not scaled down from 256 px) so the outline stays crisp; the small sizes get a bigger head
    and fewer details so the turtle still reads at 32 and 16 px.

.PARAMETER PreviewDir
    Optional folder: also writes each size as icon-<size>.png there, for a quick look.
#>
param([string]$PreviewDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\LazyTurtle\Assets\app.ico'

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function New-TurtlePng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0
    $tiny = $size -le 16          # 16 px: only the shapes that matter
    $small = $size -le 32         # 32 px and below: bigger head, no fine details

    # Bold outline: about 9 px at 256, but never thinner than one real pixel.
    $ow = [Math]::Max($(if ($tiny) { 1.0 } elseif ($small) { 1.4 } else { 2.2 }), 9 * $s)
    $ink = C 30 70 30
    $outline = New-Object System.Drawing.Pen $ink, $ow
    $outline.LineJoin = 'Round'
    $skin = New-Object System.Drawing.SolidBrush (C 150 222 95)

    # Fills a shape (rectangle in 256-px units) and outlines it.
    function Blob($brush, [double]$x, [double]$y, [double]$w, [double]$h, [switch]$NoLine) {
        $g.FillEllipse($brush, [float]($x * $s), [float]($y * $s), [float]($w * $s), [float]($h * $s))
        if (-not $NoLine) { $g.DrawEllipse($outline, [float]($x * $s), [float]($y * $s), [float]($w * $s), [float]($h * $s)) }
    }
    function RoundRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $x *= $s; $y *= $s; $w *= $s; $h *= $s; $d = 2 * $r * $s
        $p.AddArc([float]$x, [float]$y, [float]$d, [float]$d, 180, 90)
        $p.AddArc([float]($x + $w - $d), [float]$y, [float]$d, [float]$d, 270, 90)
        $p.AddArc([float]($x + $w - $d), [float]($y + $h - $d), [float]$d, [float]$d, 0, 90)
        $p.AddArc([float]$x, [float]($y + $h - $d), [float]$d, [float]$d, 90, 90)
        $p.CloseFigure()
        return , $p
    }

    # Head position and size: the small icons get a bigger head (the face is what you recognise).
    if ($small) { $hx = 112; $hy = 30; $hw = 140; $hh = 132 } else { $hx = 124; $hy = 40; $hw = 124; $hh = 118 }

    # --- legs and tail (behind the shell) ---
    if (-not $tiny) {
        $tail = New-Object System.Drawing.Drawing2D.GraphicsPath
        $tail.AddPolygon([System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF (30 * $s), (178 * $s)),
            (New-Object System.Drawing.PointF (6 * $s), (196 * $s)),
            (New-Object System.Drawing.PointF (36 * $s), (198 * $s))))
        $g.FillPath($skin, $tail); $g.DrawPath($outline, $tail)
    }
    foreach ($lx in 40, 136) {
        $leg = RoundRect $lx 176 48 52 20
        $g.FillPath($skin, $leg); $g.DrawPath($outline, $leg)
    }

    # --- shell: a bright dome with a light rim ---
    $dome = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dome.AddArc([float](22 * $s), [float](62 * $s), [float](176 * $s), [float](236 * $s), 180, 180)
    $dome.CloseFigure()
    $domeRect = New-Object System.Drawing.RectangleF (22 * $s), (62 * $s), (176 * $s), (120 * $s)
    $shellBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $domeRect, (C 120 205 70), (C 40 140 60), 90
    $g.FillPath($shellBrush, $dome)
    if (-not $small) {
        # shell plates
        $plate = New-Object System.Drawing.SolidBrush (C 185 235 110)
        Blob $plate 70 96 58 44 -NoLine
        Blob $plate 40 142 44 30 -NoLine
        Blob $plate 102 146 50 30 -NoLine
        Blob $plate 152 120 34 40 -NoLine
        $shine = New-Object System.Drawing.SolidBrush (C 255 255 255 110)
        Blob $shine 60 78 52 18 -NoLine
    }
    $g.DrawPath($outline, $dome)
    $rim = RoundRect 14 168 196 26 13
    $g.FillPath((New-Object System.Drawing.SolidBrush (C 250 228 140)), $rim)
    $g.DrawPath($outline, $rim)

    # --- yellow school backpack on the shell ---
    $yellow = New-Object System.Drawing.SolidBrush (C 255 208 40)
    $orange = New-Object System.Drawing.SolidBrush (C 245 150 30)
    if ($tiny) { $pack = RoundRect 28 82 84 84 16 } else { $pack = RoundRect 34 86 76 80 16 }
    $g.FillPath($yellow, $pack); $g.DrawPath($outline, $pack)
    if (-not $tiny) {
        # flap, pocket and buckle
        $flap = RoundRect 34 86 76 34 16
        $g.FillPath($orange, $flap); $g.DrawPath($outline, $flap)
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $ink), [float](66 * $s), [float](112 * $s), [float](12 * $s), [float](14 * $s))
        if (-not $small) {
            $pocket = RoundRect 48 132 48 24 8
            $g.FillPath((New-Object System.Drawing.SolidBrush (C 255 228 110)), $pocket); $g.DrawPath($outline, $pocket)
            # strap running across the shell to the front
            $strap = New-Object System.Drawing.Pen (C 245 150 30), ([float](10 * $s))
            $g.DrawArc($strap, [float](96 * $s), [float](84 * $s), [float](80 * $s), [float](90 * $s), 200, 110)
        }
    }

    # --- neck and head ---
    Blob $skin ($hx + 2) ($hy + $hh * 0.62) ($hw * 0.42) ($hh * 0.5)
    Blob $skin $hx $hy $hw $hh
    if (-not $small) { Blob (New-Object System.Drawing.SolidBrush (C 255 255 255 90)) ($hx + 26) ($hy + 8) 54 18 -NoLine }

    # big eyes with highlights
    $ew = $hw * 0.30; $eh = $hh * 0.38; $ey = $hy + $hh * 0.18; $pw = 0.6
    $eyes = @(($hx + $hw * 0.17), ($hx + $hw * 0.53))
    if ($tiny) { $ew = $hw * 0.34; $eh = $hh * 0.44; $ey = $hy + $hh * 0.14; $pw = 0.45; $eyes = @(($hx + $hw * 0.14), ($hx + $hw * 0.54)) }
    $pupil = New-Object System.Drawing.SolidBrush (C 25 30 40)
    $thin = New-Object System.Drawing.Pen $ink, ([float]($ow * 0.6))
    foreach ($ex in $eyes) {
        # the small icons skip the eye outline: at 16-48 px it would swallow the white
        if ($size -le 48) { Blob ([System.Drawing.Brushes]::White) $ex $ey $ew $eh -NoLine }
        else { $g.FillEllipse([System.Drawing.Brushes]::White, [float]($ex * $s), [float]($ey * $s), [float]($ew * $s), [float]($eh * $s)); $g.DrawEllipse($thin, [float]($ex * $s), [float]($ey * $s), [float]($ew * $s), [float]($eh * $s)) }
        Blob $pupil ($ex + $ew * (0.88 - $pw)) ($ey + $eh * 0.26) ($ew * $pw) ($eh * 0.62) -NoLine
        if (-not $tiny) {
            Blob ([System.Drawing.Brushes]::White) ($ex + $ew * 0.42) ($ey + $eh * 0.32) ($ew * 0.26) ($eh * 0.24) -NoLine
            Blob ([System.Drawing.Brushes]::White) ($ex + $ew * 0.62) ($ey + $eh * 0.62) ($ew * 0.12) ($eh * 0.11) -NoLine
        }
    }

    # rosy cheeks
    if (-not $tiny) {
        $cheek = New-Object System.Drawing.SolidBrush (C 255 120 140 170)
        Blob $cheek ($hx + $hw * 0.06) ($hy + $hh * 0.62) ($hw * 0.18) ($hh * 0.12) -NoLine
        Blob $cheek ($hx + $hw * 0.78) ($hy + $hh * 0.62) ($hw * 0.16) ($hh * 0.12) -NoLine
    }

    # smile: a happy open mouth at 48+ px, a simple curve when small
    $mx = $hx + $hw * 0.30; $my = $hy + $hh * 0.50; $mw = $hw * 0.40; $mh = $hh * 0.30
    if ($small) {
        $smile = New-Object System.Drawing.Pen $ink, ([float]([Math]::Max($(if ($tiny) { 1.0 } else { 1.3 }), 8 * $s)))
        $smile.StartCap = 'Round'; $smile.EndCap = 'Round'
        $g.DrawArc($smile, [float]($mx * $s), [float]($my * $s), [float]($mw * $s), [float]($mh * $s), 15, 150)
    }
    else {
        $mouth = New-Object System.Drawing.Drawing2D.GraphicsPath
        $mouth.AddArc([float]($mx * $s), [float](($my - $mh * 0.2) * $s), [float]($mw * $s), [float]($mh * $s), 0, 180)
        $mouth.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush (C 170 40 60)), $mouth)
        $tongue = New-Object System.Drawing.Region $mouth
        $g.SetClip($tongue, 'Replace')
        Blob (New-Object System.Drawing.SolidBrush (C 255 120 130)) ($mx + $mw * 0.25) ($my + $mh * 0.42) ($mw * 0.5) ($mh * 0.5) -NoLine
        $g.ResetClip()
        $g.DrawPath($outline, $mouth)
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 64, 48, 32, 16
$images = foreach ($sz in $sizes) { , (New-TurtlePng $sz) }

if ($PreviewDir) {
    New-Item -ItemType Directory -Path $PreviewDir -Force | Out-Null
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        [System.IO.File]::WriteAllBytes((Join-Path $PreviewDir "icon-$($sizes[$i]).png"), $images[$i])
    }
}

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
