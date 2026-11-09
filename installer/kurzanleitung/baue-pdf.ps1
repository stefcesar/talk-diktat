# Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
# SPDX-License-Identifier: MIT
# Erzeugt Talk-Kurzanleitung.pdf aus Kurzanleitung.html mit Microsoft Edge ohne Fenster.
# Aufruf im Projektordner: pwsh -File installer\kurzanleitung\baue-pdf.ps1 [-Ziel Ordner] [-Quelle Datei.html -Name Datei.pdf]
# Ohne Quelle und Name die Kurzanleitung; mit -Quelle und -Name ein anderes HTML-Blatt.
param(
    [string]$Ziel = (Join-Path $PSScriptRoot 'ausgabe'),
    [string]$Quelle = 'Kurzanleitung.html',
    [string]$Name = 'Talk-Kurzanleitung.pdf'
)
$ErrorActionPreference = 'Stop'

$edge = @(
    'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
    'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge nicht gefunden.' }

$html = Join-Path $PSScriptRoot $Quelle
New-Item -ItemType Directory -Force $Ziel | Out-Null
$pdf = Join-Path $Ziel $Name

# Ein eigenes Profil im Temp Ordner, damit Edge nicht das Profil des Nutzers anfasst.
$profil = Join-Path ([IO.Path]::GetTempPath()) 'talk-pdf-profil'
$p = Start-Process $edge -ArgumentList @(
    '--headless', '--disable-gpu', '--no-pdf-header-footer', "--user-data-dir=$profil",
    "--print-to-pdf=$pdf", ([Uri]$html).AbsoluteUri
) -PassThru -Wait
if (-not (Test-Path $pdf)) { throw "Das PDF fehlt (Edge Exit $($p.ExitCode))." }
"PDF: $pdf ($([math]::Round((Get-Item $pdf).Length / 1KB, 0)) KB)"
