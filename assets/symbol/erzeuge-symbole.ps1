# Erzeugt die fertigen ICO Dateien für Talk (dunkle Version: weißes Zeichen, dunkle Kachel).
# Braucht nur PowerShell 7 unter Windows. Aufruf: pwsh -File assets\symbol\erzeuge-symbole.ps1
# Die kleinen Größen sind von Hand auf ganze Pixel gesetzt, die großen werden geglättet gezeichnet.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ziel = Join-Path $PSScriptRoot 'fertig'
New-Item -ItemType Directory -Force $ziel | Out-Null

$tinte  = [Drawing.ColorTranslator]::FromHtml('#1B1F24')
$weiss  = [Drawing.Color]::White
$akzent = [Drawing.ColorTranslator]::FromHtml('#FF6A3D')

# Je Kantenlänge: Strichstärke, Rand, Höhe des linken und des rechten Pegelbalkens.
$trayMasse = @{ 16 = 2, 1, 8, 4; 20 = 2, 1, 10, 4; 24 = 3, 2, 12, 6; 32 = 4, 2, 16, 8 }
$appMasse  = @{ 16 = 2, 2, 6, 4; 20 = 2, 3, 8, 4; 24 = 2, 3, 10, 4; 32 = 3, 4, 12, 6 }
$appGross  = 40, 48, 64, 256
$zustaende = 'ruhe', 'aufnahme', 'verarbeitung', 'fehler'

function New-Bild([int]$n) {
    [Drawing.Bitmap]::new($n, $n, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Add-ZeichenRaster($g, [int]$n, [string]$zustand, $farbe, [int[]]$mass) {
    $w, $m, $hL, $hR = $mass
    $pinsel = [Drawing.SolidBrush]::new($farbe)
    $akzentPinsel = [Drawing.SolidBrush]::new($akzent)
    $g.SmoothingMode = 'None'
    $g.PixelOffsetMode = 'None'

    $breite = $n - 2 * $m
    if ((($breite - $w) % 2) -ne 0) { $breite-- }
    $h = $n - 2 * $m
    $stammX = if ($zustand -eq 'verarbeitung') { $m + $w } else { $m + ($breite - $w) / 2 }
    $g.FillRectangle($pinsel, [int]$stammX, $m, $w, $h)
    $g.FillRectangle($pinsel, [int]($stammX - $w), $m, 3 * $w, $w)
    $g.FillRectangle($pinsel, [int]($stammX - $w), $m + $h - $w, 3 * $w, $w)

    $rechtsX = $m + $breite - $w
    $punktY = $m + [int][math]::Floor(($h - $w) / 2)
    switch ($zustand) {
        'ruhe' {
            $g.FillRectangle($pinsel, $m, $punktY, $w, $w)
            $g.FillRectangle($pinsel, $rechtsX, $punktY, $w, $w)
        }
        'aufnahme' {
            $g.FillRectangle($akzentPinsel, $m, [int]($m + ($h - $hL) / 2), $w, $hL)
            $g.FillRectangle($akzentPinsel, $rechtsX, [int]($m + ($h - $hR) / 2), $w, $hR)
        }
        'verarbeitung' {
            $luecke = if ($n -le 20) { 1 } else { [int][math]::Ceiling($w / 2) }
            $start = $n - [math]::Max(0, $m - 1) - (3 * $w + 2 * $luecke)
            foreach ($i in 0..2) { $g.FillRectangle($pinsel, $start + $i * ($w + $luecke), $punktY, $w, $w) }
        }
        'fehler' {
            $g.SmoothingMode = 'AntiAlias'
            $stift = [Drawing.Pen]::new($akzent, [single]$w)
            $a = [single]($m + $w / 2)
            $b = [single]($n - $m - $w / 2)
            $g.DrawLine($stift, $a, $b, $b, $a)
        }
    }
}

function Add-RundRechteck($g, $pinsel, [single]$x, [single]$y, [single]$b, [single]$h, [single]$r) {
    $d = 2 * $r
    $pfad = [Drawing.Drawing2D.GraphicsPath]::new()
    $pfad.AddArc($x, $y, $d, $d, 180, 90)
    $pfad.AddArc($x + $b - $d, $y, $d, $d, 270, 90)
    $pfad.AddArc($x + $b - $d, $y + $h - $d, $d, $d, 0, 90)
    $pfad.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $pfad.CloseFigure()
    $g.FillPath($pinsel, $pfad)
}

function Add-Kachel($g, [int]$n) {
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    Add-RundRechteck $g ([Drawing.SolidBrush]::new($tinte)) 0 0 $n $n ($n * 56 / 256)
}

# Große Zeichnung auf dem Raster 256 (wie app-256.svg), in die Kachel eingepasst.
function Add-ZeichenVektor($g, [int]$n) {
    $s = $n / 256 * 0.625
    $v = $n / 256 * 48
    $weissPinsel = [Drawing.SolidBrush]::new($weiss)
    $akzentPinsel = [Drawing.SolidBrush]::new($akzent)
    $teile = @(
        @($weissPinsel, 112, 16, 32, 224), @($weissPinsel, 80, 16, 96, 32), @($weissPinsel, 80, 208, 96, 32),
        @($akzentPinsel, 16, 64, 32, 128), @($akzentPinsel, 208, 96, 32, 64)
    )
    foreach ($t in $teile) {
        Add-RundRechteck $g $t[0] ($v + $t[1] * $s) ($v + $t[2] * $s) ($t[3] * $s) ($t[4] * $s) (16 * $s)
    }
}

function Get-PngBytes($bild) {
    $strom = [IO.MemoryStream]::new()
    $bild.Save($strom, [Drawing.Imaging.ImageFormat]::Png)
    , $strom.ToArray()
}

# Unkomprimiertes Bild für ICO: Kopf, Pixel von unten nach oben, leere Maske.
function Get-DibBytes($bild) {
    $n = $bild.Width
    $strom = [IO.MemoryStream]::new()
    $bw = [IO.BinaryWriter]::new($strom)
    $bw.Write([int]40); $bw.Write([int]$n); $bw.Write([int](2 * $n))
    $bw.Write([int16]1); $bw.Write([int16]32)
    foreach ($i in 1..6) { $bw.Write([int]0) }
    for ($y = $n - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $n; $x++) {
            $c = $bild.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $maskenZeile = [int]([math]::Ceiling($n / 32) * 4)
    $bw.Write([byte[]]::new($maskenZeile * $n))
    $bw.Flush()
    , $strom.ToArray()
}

function Write-Ico([string]$name, $bilder) {
    $daten = foreach ($b in $bilder) {
        if ($b.Width -ge 256) { , (Get-PngBytes $b) } else { , (Get-DibBytes $b) }
    }
    $daten = @($daten)
    $strom = [IO.MemoryStream]::new()
    $bw = [IO.BinaryWriter]::new($strom)
    $bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$bilder.Count)
    $versatz = 6 + 16 * $bilder.Count
    for ($i = 0; $i -lt $bilder.Count; $i++) {
        $kante = if ($bilder[$i].Width -ge 256) { 0 } else { $bilder[$i].Width }
        $bw.Write([byte]$kante); $bw.Write([byte]$kante); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([int16]1); $bw.Write([int16]32)
        $bw.Write([int]$daten[$i].Length); $bw.Write([int]$versatz)
        $versatz += $daten[$i].Length
    }
    foreach ($d in $daten) { $bw.Write([byte[]]$d) }
    $bw.Flush()
    [IO.File]::WriteAllBytes((Join-Path $ziel $name), $strom.ToArray())
}

$reihen = [ordered]@{}

$app = @()
foreach ($n in $appMasse.Keys | Sort-Object) {
    $bild = New-Bild $n
    $g = [Drawing.Graphics]::FromImage($bild)
    Add-Kachel $g $n
    Add-ZeichenRaster $g $n 'aufnahme' $weiss $appMasse[$n]
    $g.Dispose()
    $app += $bild
}
foreach ($n in $appGross) {
    $bild = New-Bild $n
    $g = [Drawing.Graphics]::FromImage($bild)
    Add-Kachel $g $n
    Add-ZeichenVektor $g $n
    $g.Dispose()
    $app += $bild
}
Write-Ico 'talk.ico' $app
$reihen['talk'] = $app

foreach ($z in $zustaende) {
    $bilder = @()
    foreach ($n in $trayMasse.Keys | Sort-Object) {
        $bild = New-Bild $n
        $g = [Drawing.Graphics]::FromImage($bild)
        Add-ZeichenRaster $g $n $z $weiss $trayMasse[$n]
        $g.Dispose()
        $bilder += $bild
    }
    Write-Ico "tray-$z.ico" $bilder
    $reihen["tray-$z"] = $bilder
}

# Kontrollblatt: jede Reihe einmal in Originalgröße und einmal vierfach vergrößert (ohne die 256er Stufe).
$blatt = [Drawing.Bitmap]::new(1100, 1500)
$g = [Drawing.Graphics]::FromImage($blatt)
$g.Clear([Drawing.ColorTranslator]::FromHtml('#202020'))
$g.InterpolationMode = 'NearestNeighbor'
$g.PixelOffsetMode = 'Half'
$y = 20
foreach ($name in $reihen.Keys) {
    $x = 20
    foreach ($b in $reihen[$name]) { if ($b.Width -lt 256) { $g.DrawImage($b, $x, $y, $b.Width, $b.Height); $x += $b.Width + 12 } }
    $y += 76
    $x = 20
    foreach ($b in $reihen[$name]) { if ($b.Width -lt 256) { $g.DrawImage($b, $x, $y, 4 * $b.Width, 4 * $b.Height); $x += 4 * $b.Width + 24 } }
    $y += $(if ($name -eq 'talk') { 280 } else { 150 })
}
$g.DrawImage($reihen['talk'][-1], 820, 420, 256, 256)
$g.Dispose()
$blatt.Save((Join-Path $ziel 'kontrolle.png'), [Drawing.Imaging.ImageFormat]::Png)

Get-ChildItem $ziel | ForEach-Object { '{0,-24} {1,8} Bytes' -f $_.Name, $_.Length }
