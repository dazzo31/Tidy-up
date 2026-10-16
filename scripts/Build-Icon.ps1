<#
.SYNOPSIS
    Renders the TidyUp mark to a multi-resolution Windows icon (Resources\tidyup.ico).

.DESCRIPTION
    Draws the same geometry as TidyUp\Resources\Brand\tidyup.svg (256x256 grid) with System.Drawing, so no
    external tools are needed. Each size is rendered at 4x and downscaled for clean edges, and is stored as a
    PNG-compressed ICO image. Sizes: 16, 20, 24, 32, 40, 48, 64, 128, 256.

    Small sizes thicken the check stroke so it stays readable at 16 px.

    If you edit the SVG, update the geometry constants below to match, then re-run:
        pwsh -File scripts\Build-Icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutFile
)

if (-not $OutFile) {
    $OutFile = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\TidyUp\Resources\tidyup.ico'
}

Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$accent = [System.Drawing.ColorTranslator]::FromHtml('#0067C0')
$white = [System.Drawing.Color]::White

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $d = 2 * $r
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Render-Mark([int]$size) {
    $ss = 4
    $px = $size * $ss
    $scale = $px / 256.0
    $big = New-Object System.Drawing.Bitmap $px, $px, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($scale, $scale)

    $accentBrush = New-Object System.Drawing.SolidBrush $accent
    $whiteBrush = New-Object System.Drawing.SolidBrush $white

    $tile = New-RoundedRect 16 16 224 224 52
    $g.FillPath($accentBrush, $tile)
    $tab = New-RoundedRect 52 68 68 40 12
    $g.FillPath($whiteBrush, $tab)
    $body = New-RoundedRect 52 88 152 104 14
    $g.FillPath($whiteBrush, $body)

    # Check stroke: 18 units on the 256 grid, but never thinner than ~1.6 output pixels.
    $stroke = [Math]::Max(18.0, 1.6 * 256.0 / $size)
    $pen = New-Object System.Drawing.Pen $accent, $stroke
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF 92, 140),
            (New-Object System.Drawing.PointF 118, 166),
            (New-Object System.Drawing.PointF 166, 114)))
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $og = [System.Drawing.Graphics]::FromImage($out)
    $og.InterpolationMode = 'HighQualityBicubic'
    $og.PixelOffsetMode = 'HighQuality'
    $og.CompositingQuality = 'HighQuality'
    $og.DrawImage($big, 0, 0, $size, $size)
    $og.Dispose(); $big.Dispose()
    return $out
}

$images = foreach ($s in $sizes) {
    $bmp = Render-Mark $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0)               # reserved
$bw.Write([uint16]1)               # type: icon
$bw.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($i in $images) {
    $dim = if ($i.Size -ge 256) { 0 } else { $i.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)       # palette, reserved
    $bw.Write([uint16]1); $bw.Write([uint16]32)  # planes, bpp
    $bw.Write([uint32]$i.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $i.Data.Length
}
foreach ($i in $images) { $bw.Write($i.Data) }
$bw.Flush()

$target = [System.IO.Path]::GetFullPath($OutFile)
[System.IO.File]::WriteAllBytes($target, $ms.ToArray())
Write-Output ("Wrote {0} ({1:N0} bytes, {2} images: {3})" -f $target, $ms.Length, $images.Count, ($sizes -join ','))
