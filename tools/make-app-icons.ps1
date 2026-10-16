# Erzeugt alle App-Icons (Kacheln, Taskleiste, Splash, .ico) im Verlaufs-Design.
# Aufruf: powershell -File tools\make-app-icons.ps1
Add-Type -AssemblyName System.Drawing

$assets = Join-Path $PSScriptRoot "..\PasswordVault.App\Assets"
$c1 = [System.Drawing.Color]::FromArgb(255, 139, 92, 246)   # Violett
$c2 = [System.Drawing.Color]::FromArgb(255, 99, 102, 241)   # Indigo
$c3 = [System.Drawing.Color]::FromArgb(255, 6, 182, 212)    # Cyan

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

function New-GradientBrush([float]$x, [float]$y, [float]$w, [float]$h) {
    $b = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF $x, $y), (New-Object System.Drawing.PointF ($x + $w), ($y + $h)), $c1, $c3)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @($c1, $c2, $c3)
    $blend.Positions = @(0.0, 0.5, 1.0)
    $b.InterpolationColors = $blend
    return $b
}

# Weißes Schloss, zentriert in (cx, cy) mit Kantenlänge s
function Draw-Lock($g, [float]$cx, [float]$cy, [float]$s, [bool]$keyhole) {
    $white = [System.Drawing.Color]::White
    $pen = New-Object System.Drawing.Pen $white, ([Math]::Max(1.4, $s * 0.085))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $x0 = $cx - $s / 2; $y0 = $cy - $s / 2
    $bodyW = $s * 0.50; $bodyH = $s * 0.36
    $bodyX = $x0 + ($s - $bodyW) / 2; $bodyY = $y0 + $s * 0.46
    $shW = $s * 0.30; $shX = $x0 + ($s - $shW) / 2; $shTop = $y0 + $s * 0.22
    $g.DrawArc($pen, $shX, $shTop, $shW, $shW, 180, 180)
    $g.DrawLine($pen, $shX, $shTop + $shW / 2, $shX, $bodyY)
    $g.DrawLine($pen, $shX + $shW, $shTop + $shW / 2, $shX + $shW, $bodyY)
    $g.FillPath((New-Object System.Drawing.SolidBrush $white), (New-RoundedRect $bodyX $bodyY $bodyW $bodyH ($s * 0.08)))
    if ($keyhole) {
        $r = $s * 0.05; $hx = $cx; $hy = $bodyY + $bodyH * 0.45
        $hb = New-Object System.Drawing.SolidBrush $c2
        $g.FillEllipse($hb, $hx - $r, $hy - $r, $r * 2, $r * 2)
        $g.FillRectangle($hb, $hx - $r * 0.45, $hy, $r * 0.9, $bodyH * 0.28)
    }
}

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($bmp, $g)
}

# App-Symbol: abgerundete Verlaufskachel mit Schloss (füllt die Fläche mit kleinem Rand)
function New-AppIcon([int]$size) {
    $bmp, $g = New-Canvas $size $size
    $m = [Math]::Max(0, [Math]::Round($size * 0.04))
    $s = $size - 2 * $m
    $g.FillPath((New-GradientBrush $m $m $s $s), (New-RoundedRect $m $m ($s - 0.5) ($s - 0.5) ($s * 0.26)))
    Draw-Lock $g ($size / 2) ($size / 2) $s ($size -ge 32)
    $g.Dispose()
    return $bmp
}

function Save($bmp, $name) {
    $bmp.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    "  $name ($($bmp.Width)x$($bmp.Height))"
    $bmp.Dispose()
}

# Volle Verlaufsfläche mit Schloss (Kacheln im Startmenü)
function New-Tile([int]$w, [int]$h, [float]$lockSize, [string]$text) {
    $bmp, $g = New-Canvas $w $h
    $g.FillRectangle((New-GradientBrush 0 0 $w $h), 0, 0, $w, $h)
    if ($text) {
        $font = New-Object System.Drawing.Font("Segoe UI Semibold", ($h * 0.11), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $ts = $g.MeasureString($text, $font)
        $gap = $h * 0.06
        $visible = $lockSize * 0.5   # sichtbare Breite des Schlosses
        $total = $visible + $gap + $ts.Width
        $lx = ($w - $total) / 2 + $visible / 2
        Draw-Lock $g $lx ($h / 2) $lockSize $true
        $g.DrawString($text, $font, [System.Drawing.Brushes]::White, ($lx + $visible / 2 + $gap), (($h - $ts.Height) / 2))
    } else {
        Draw-Lock $g ($w / 2) ($h / 2) $lockSize $true
    }
    $g.Dispose()
    return $bmp
}

"Erzeuge Icons in $assets"

# Taskleiste / Startmenü-Liste (Square44, mehrere Zielgrößen)
foreach ($t in 16, 24, 32, 48, 256) {
    Save (New-AppIcon $t) "Square44x44Logo.targetsize-$t.png"
    Save (New-AppIcon $t) "Square44x44Logo.targetsize-$($t)_altform-unplated.png"
}
Save (New-AppIcon 88) "Square44x44Logo.scale-200.png"
Save (New-AppIcon 44) "Square44x44Logo.scale-100.png"

# Kacheln
Save (New-Tile 300 300 150 $null) "Square150x150Logo.scale-200.png"
Save (New-Tile 150 150 75 $null) "Square150x150Logo.scale-100.png"
Save (New-Tile 620 300 130 "Password Vault") "Wide310x150Logo.scale-200.png"
Save (New-Tile 310 150 65 "Password Vault") "Wide310x150Logo.scale-100.png"

# Store-Logo & Sperrbildschirm
Save (New-AppIcon 50) "StoreLogo.png"
Save (New-AppIcon 48) "LockScreenLogo.scale-200.png"

# Splashscreen: transparent, Logo mittig
$bmp, $g = New-Canvas 1240 600
$tile = 300
$g.FillPath((New-GradientBrush ((1240 - $tile) / 2) ((600 - $tile) / 2) $tile $tile),
    (New-RoundedRect ((1240 - $tile) / 2) ((600 - $tile) / 2) $tile $tile ($tile * 0.26)))
Draw-Lock $g 620 300 $tile $true
$g.Dispose()
Save $bmp "SplashScreen.scale-200.png"

# Fenster-/Programm-Symbol (.ico mit PNG-Einträgen)
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($t in $sizes) {
    $b = New-AppIcon $t
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
    , $ms.ToArray()
}
$icoPath = Join-Path $assets "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
"  app.ico ($($sizes -join ', '))"
