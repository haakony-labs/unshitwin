# Regenerates src/UnshitWindows/app.ico (tray + exe icon).
# Sizes below 256 are stored as 32-bit DIBs, 256 as PNG, which every Windows icon loader accepts.
param([string]$OutFile = (Join-Path $PSScriptRoot '..\src\UnshitWindows\app.ico'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$s) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $s / 16.0

    # Blue rounded square.
    $bg = New-RoundedRect (0.5 * $u) (0.5 * $u) (15 * $u) (15 * $u) (3.5 * $u)
    $full = [System.Drawing.RectangleF]::new(0, 0, $s, $s)
    $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new($full,
        [System.Drawing.Color]::FromArgb(255, 59, 130, 246),
        [System.Drawing.Color]::FromArgb(255, 29, 78, 216), [single]90)
    $g.FillPath($grad, $bg)

    # Back window: outline.
    $back = New-RoundedRect (2.8 * $u) (3 * $u) (7.6 * $u) (6.2 * $u) (1.1 * $u)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(215, 255, 255, 255), [single][math]::Max(1.0, 1.1 * $u))
    $g.DrawPath($pen, $back)

    # Front window: filled, with a light title bar.
    $front = New-RoundedRect (5.8 * $u) (6.6 * $u) (7.4 * $u) (6.2 * $u) (1.1 * $u)
    $g.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::White), $front)
    $g.SetClip($front)
    $g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 147, 197, 253)),
        [single](5.8 * $u), [single](6.6 * $u), [single](7.4 * $u), [single](1.9 * $u))
    $g.ResetClip()

    $g.Dispose()
    return $bmp
}

$entries = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 256) {
        $ms = [System.IO.MemoryStream]::new()
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $data = $ms.ToArray()
    }
    else {
        $rect = [System.Drawing.Rectangle]::new(0, 0, $s, $s)
        $bd = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $stride = $bd.Stride
        $pixels = [byte[]]::new($stride * $s)
        [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $pixels, 0, $pixels.Length)
        $bmp.UnlockBits($bd)

        $maskRow = [int]([math]::Ceiling($s / 32.0) * 4)
        $ms = [System.IO.MemoryStream]::new()
        $w = [System.IO.BinaryWriter]::new($ms)
        # BITMAPINFOHEADER; height is doubled to cover the AND mask.
        $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2))
        $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]0)
        $w.Write([int]($s * 4 * $s + $maskRow * $s))
        $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
        for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $stride, $s * 4) }   # bottom-up
        $w.Write([byte[]]::new($maskRow * $s))                                          # AND mask unused (alpha)
        $w.Flush()
        $data = $ms.ToArray()
    }
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $data }
}

$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$e.Data.Length); $w.Write([int]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) { $w.Write([byte[]]$e.Data) }
$w.Flush()

$full = [System.IO.Path]::GetFullPath($OutFile)
[System.IO.File]::WriteAllBytes($full, $out.ToArray())
"Wrote $full ($($out.Length) bytes, sizes: $($sizes -join ', '))"
