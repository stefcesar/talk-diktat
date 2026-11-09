# Erzeugt die SVG Entwürfe für das Talk Symbol (Richtung B, Stimme wird Cursor).
# Die Geometrie steht nur hier. Aufruf: pwsh -File assets\symbol\erzeuge-entwuerfe.ps1
$ErrorActionPreference = 'Stop'
$ziel = $PSScriptRoot

$tinte  = '#1B1F24'
$weiss  = '#FFFFFF'
$akzent = '#FF6A3D'
$zustaende = 'ruhe', 'aufnahme', 'verarbeitung', 'fehler'

# Kleine Zeichnung: Raster 16, nur ganze Pixel, keine Rundungen.
function Get-ZeichenKlein([string]$zustand, [string]$farbe) {
    $cursorMitte = '<rect x="7" y="1" width="2" height="14"/><rect x="5" y="1" width="6" height="2"/><rect x="5" y="13" width="6" height="2"/>'
    $cursorLinks = '<rect x="3" y="1" width="2" height="14"/><rect x="1" y="1" width="6" height="2"/><rect x="1" y="13" width="6" height="2"/>'
    $inhalt = switch ($zustand) {
        'ruhe'         { "$cursorMitte<rect x=""1"" y=""7"" width=""2"" height=""2""/><rect x=""13"" y=""7"" width=""2"" height=""2""/>" }
        'aufnahme'     { "$cursorMitte<g fill=""$akzent""><rect x=""1"" y=""4"" width=""2"" height=""8""/><rect x=""13"" y=""6"" width=""2"" height=""4""/></g>" }
        'verarbeitung' { "$cursorLinks<rect x=""8"" y=""7"" width=""2"" height=""2""/><rect x=""11"" y=""7"" width=""2"" height=""2""/><rect x=""14"" y=""7"" width=""2"" height=""2""/>" }
        'fehler'       { "$cursorMitte<path d=""M2.5 13.5 L13.5 2.5"" stroke=""$akzent"" stroke-width=""2"" fill=""none"" shape-rendering=""geometricPrecision""/>" }
    }
    "<g fill=""$farbe"">$inhalt</g>"
}

# Große Zeichnung: Raster 256, runde Enden.
function Get-ZeichenGross([string]$zustand, [string]$farbe) {
    $cursorMitte = '<rect x="112" y="16" width="32" height="224" rx="16"/><rect x="80" y="16" width="96" height="32" rx="16"/><rect x="80" y="208" width="96" height="32" rx="16"/>'
    $cursorLinks = '<rect x="48" y="16" width="32" height="224" rx="16"/><rect x="16" y="16" width="96" height="32" rx="16"/><rect x="16" y="208" width="96" height="32" rx="16"/>'
    $inhalt = switch ($zustand) {
        'ruhe'         { "$cursorMitte<circle cx=""32"" cy=""128"" r=""16""/><circle cx=""224"" cy=""128"" r=""16""/>" }
        'aufnahme'     { "$cursorMitte<g fill=""$akzent""><rect x=""16"" y=""64"" width=""32"" height=""128"" rx=""16""/><rect x=""208"" y=""96"" width=""32"" height=""64"" rx=""16""/></g>" }
        'verarbeitung' { "$cursorLinks<circle cx=""144"" cy=""128"" r=""16""/><circle cx=""192"" cy=""128"" r=""16""/><circle cx=""240"" cy=""128"" r=""16""/>" }
        'fehler'       { "$cursorMitte<path d=""M40 216 L216 40"" stroke=""$akzent"" stroke-width=""32"" stroke-linecap=""round"" fill=""none""/>" }
    }
    "<g fill=""$farbe"">$inhalt</g>"
}

function Write-Svg([string]$name, [int]$kante, [string]$inhalt, [bool]$scharf) {
    $rendering = if ($scharf) { ' shape-rendering="crispEdges"' } else { '' }
    $svg = "<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 $kante $kante"" width=""$kante"" height=""$kante""$rendering>$inhalt</svg>`n"
    Set-Content -Path (Join-Path $ziel $name) -Value $svg -NoNewline -Encoding utf8
}

foreach ($z in $zustaende) {
    # "dunkel" und "hell" meinen die Taskleiste, auf der das Zeichen liegt.
    Write-Svg "tray-$z-dunkel-16.svg" 16 (Get-ZeichenKlein $z $weiss) $true
    Write-Svg "tray-$z-hell-16.svg"   16 (Get-ZeichenKlein $z $tinte) $true
    Write-Svg "zeichen-$z-256.svg"    256 (Get-ZeichenGross $z $tinte) $false
}

# Programmsymbol: dunkle Kachel, weißer Cursor, Pegelbalken in der Akzentfarbe.
$appKlein = "<rect width=""16"" height=""16"" rx=""3"" fill=""$tinte"" shape-rendering=""geometricPrecision""/>" +
    "<g fill=""$weiss""><rect x=""7"" y=""3"" width=""2"" height=""10""/><rect x=""5"" y=""3"" width=""6"" height=""1""/><rect x=""5"" y=""12"" width=""6"" height=""1""/></g>" +
    "<g fill=""$akzent""><rect x=""2"" y=""5"" width=""2"" height=""6""/><rect x=""12"" y=""6"" width=""2"" height=""4""/></g>"
Write-Svg 'app-16.svg' 16 $appKlein $true

$appGross = "<rect width=""256"" height=""256"" rx=""56"" fill=""$tinte""/>" +
    "<g transform=""translate(48 48) scale(0.625)"">$(Get-ZeichenGross 'aufnahme' $weiss)</g>"
Write-Svg 'app-256.svg' 256 $appGross $false

Write-Output "Entwürfe geschrieben nach $ziel"
