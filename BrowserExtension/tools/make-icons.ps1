# Erzeugt die Erweiterungs-Icons (16/32/48/128 px) im Stil der App.
# Aufruf: powershell -File tools\make-icons.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\icons"
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($size in 16, 32, 48, 128) {
    $s = [float]$size
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Hintergrund: Verlauf Violett → Indigo → Cyan
    $tile = New-RoundedRect 0 0 ($s - 0.5) ($s - 0.5) ($s * 0.28)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s),
        [System.Drawing.Color]::FromArgb(255, 139, 92, 246), [System.Drawing.Color]::FromArgb(255, 6, 182, 212))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @([System.Drawing.Color]::FromArgb(255, 139, 92, 246), [System.Drawing.Color]::FromArgb(255, 99, 102, 241), [System.Drawing.Color]::FromArgb(255, 6, 182, 212))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $brush.InterpolationColors = $blend
    $g.FillPath($brush, $tile)

    # Schloss in Weiß
    $white = [System.Drawing.Color]::White
    $stroke = [Math]::Max(1.6, $s * 0.085)
    $pen = New-Object System.Drawing.Pen $white, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $bodyW = $s * 0.50; $bodyH = $s * 0.36
    $bodyX = ($s - $bodyW) / 2; $bodyY = $s * 0.46
    $shW = $s * 0.30; $shX = ($s - $shW) / 2; $shTop = $s * 0.22
    $g.DrawArc($pen, $shX, $shTop, $shW, $shW, 180, 180)
    $g.DrawLine($pen, $shX, $shTop + $shW / 2, $shX, $bodyY)
    $g.DrawLine($pen, $shX + $shW, $shTop + $shW / 2, $shX + $shW, $bodyY)

    $body = New-RoundedRect $bodyX $bodyY $bodyW $bodyH ($s * 0.08)
    $g.FillPath((New-Object System.Drawing.SolidBrush $white), $body)

    # Schlüsselloch im Verlaufston
    if ($size -ge 32) {
        $holeR = $s * 0.05
        $hx = $s / 2; $hy = $bodyY + $bodyH * 0.45
        $holeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 99, 102, 241))
        $g.FillEllipse($holeBrush, $hx - $holeR, $hy - $holeR, $holeR * 2, $holeR * 2)
        $g.FillRectangle($holeBrush, $hx - $holeR * 0.45, $hy, $holeR * 0.9, $bodyH * 0.28)
    }

    $path = Join-Path $outDir "icon$size.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "erstellt: $path"
}
