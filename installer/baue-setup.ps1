# Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
# SPDX-License-Identifier: MIT
# Baut das Programm als Ordner ohne Vorinstallation und daraus die Setup Datei.
# Aufruf im Projektordner: pwsh -File installer\baue-setup.ps1            (alles)
#                          pwsh -File installer\baue-setup.ps1 -NurOrdner (nur der Programmordner, ohne Inno Setup)
#                          pwsh -File installer\baue-setup.ps1 -Komplett  (Setup zur Weitergabe, mit Modellen und VC Laufzeit)
# Vor dem Aufruf muss Talk beendet sein. Ausgabe standardmäßig im Ordner ausgabe neben dem Projekt, mit -Ziel änderbar.
param(
    [switch]$NurOrdner,
    [switch]$Komplett,
    [string]$Ziel = (Join-Path (Split-Path -Parent $PSScriptRoot) 'ausgabe'),
    [string]$Modelle = (Join-Path $env:LOCALAPPDATA 'Talk\models'),
    [string]$VcRedist = (Join-Path (Split-Path -Parent $PSScriptRoot) 'ausgabe\downloads\vc_redist.x64.exe')
)
$ErrorActionPreference = 'Stop'

if (Get-Process Talk -ErrorAction SilentlyContinue) { throw 'Talk läuft noch. Bitte zuerst über das Tray Menü beenden.' }

$projekt = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $Ziel 'publish'
$setup   = Join-Path $Ziel 'setup'

# Die Version steht an einer Stelle, in TalkInfo.
$info = Get-Content (Join-Path $projekt 'src\Talk.Core\TalkInfo.cs') -Raw
if ($info -notmatch 'Version\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'Version in TalkInfo.cs nicht gefunden.' }
$version = $Matches[1]

# Gelöscht wird nur ein Ordner, der wirklich ein früherer Programmordner von Talk ist (oder leer). Alles andere bleibt unangetastet.
if (Test-Path $publish) {
    $istTalk = Test-Path (Join-Path $publish 'Talk.exe')
    $istLeer = -not (Get-ChildItem $publish -Force | Select-Object -First 1)
    if (-not ($istTalk -or $istLeer)) { throw "$publish ist kein Programmordner von Talk. Bitte selbst prüfen und leeren." }
    Remove-Item $publish -Recurse -Force
}
dotnet publish (Join-Path $projekt 'src\Talk.App\Talk.App.csproj') -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist gescheitert.' }

# Bibliotheken für andere Betriebssysteme braucht Talk nicht (allein die Grafikbibliothek für Linux wiegt 56 MB).
$fremd = Get-ChildItem (Join-Path $publish 'runtimes') -Recurse -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^(linux|osx|macos|android|ios|tvos|maccatalyst|browser)' }
foreach ($ordner in $fremd) {
    if (Test-Path $ordner.FullName) { Remove-Item $ordner.FullName -Recurse -Force }
}

$exe = Join-Path $publish 'Talk.exe'
if (-not (Test-Path $exe)) { throw 'Talk.exe fehlt im Programmordner.' }

# Der Programmordner muss ohne installiertes .NET starten: Selbsttest aus genau diesem Ordner.
$p = Start-Process $exe -ArgumentList '--selftest' -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "Selbsttest aus dem Programmordner endet mit $($p.ExitCode)." }

$groesse = [math]::Round(((Get-ChildItem $publish -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 0)
"Programmordner: $publish ($groesse MB, Version $version, Selbsttest bestanden)"
if ($NurOrdner) { return }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup *\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup *\ISCC.exe',
    'C:\Program Files\Inno Setup *\ISCC.exe'
) | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } | Select-Object -First 1 -ExpandProperty FullName
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) nicht gefunden. Installieren mit: winget install --id JRSoftware.InnoSetup -e' }

$argumente = @("/DPublishDir=$publish", "/DOutputDir=$setup", "/DAppVersion=$version")
$name = "TalkSetup-$version.exe"
if ($Komplett) {
    # Die Variante zur Weitergabe nimmt die beiden Standardmodelle und die VC Laufzeit mit. Beides muss da sein, die Laufzeit
    # muss von Microsoft signiert sein. Andere Dateien aus dem Modellordner (zum Beispiel ein größeres Modell) kommen nicht mit.
    foreach ($modell in 'ggml-silero-v6.2.0.bin', 'ggml-large-v3-turbo-q5_0.bin') {
        if (-not (Test-Path (Join-Path $Modelle $modell))) { throw "Modell fehlt: $modell in $Modelle" }
    }
    if (-not (Test-Path $VcRedist)) { throw "VC Laufzeit fehlt: $VcRedist" }
    $signatur = Get-AuthenticodeSignature $VcRedist
    if ($signatur.Status -ne 'Valid' -or $signatur.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'Die VC Laufzeit ist nicht gültig von Microsoft signiert.'
    }
    # Es muss die x64 Laufzeit sein und mindestens 14.44, sonst installiert das Setup eine, mit der Talk nicht läuft (Talk.iss, VcRedistNeeded).
    $info = (Get-Item $VcRedist).VersionInfo
    if ($info.ProductName -notmatch 'x64' -or [version]$info.ProductVersion -lt [version]'14.44') {
        throw "Die VC Laufzeit passt nicht (gefunden: $($info.ProductName), $($info.ProductVersion); nötig: x64 ab 14.44)."
    }
    $argumente += @('/DKomplett', "/DModelsDir=$Modelle", "/DVcRedist=$VcRedist")
    $name = "TalkSetup-$version-komplett.exe"
}

New-Item -ItemType Directory -Force $setup | Out-Null
& $iscc @argumente (Join-Path $PSScriptRoot 'Talk.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup ist gescheitert.' }

$datei = Join-Path $setup $name
if (-not (Test-Path $datei)) { throw 'Die Setup Datei fehlt.' }
"Setup Datei: $datei ($([math]::Round((Get-Item $datei).Length / 1MB, 0)) MB)"
