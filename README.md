# Talk – Diktieren in jedes Programm unter Windows

Talk läuft unsichtbar im Hintergrund. Zweimal kurz auf **Caps Lock** tippen, sprechen, einmal tippen – und der Text steht dort, wo du gerade schreibst: in Word, im Browser, in WhatsApp, in jedem Textfeld.

- Erkennt **auf deinem Rechner**, ganz ohne Internet (Whisper, auf der Grafikkarte wenn vorhanden)
- Optional schneller über **Groq** mit eigenem Schlüssel, dazu „Glätten“ (Text sauber ausformulieren)
- Schalter **„Nur lokal“**: dann verlässt garantiert nichts deinen Rechner
- Eigenes **Wörterbuch** für Namen, Marken und Fachwörter
- Deutsch, Italienisch, Englisch, Schweizerdeutsch und Mundart
- **Mit einer Taste nacheinander bedienbar**: keine Tastenkombinationen nötig, große Klickflächen – auch mit Bildschirmtastatur gut nutzbar

> **Status: Beta.** Getestet nur unter Windows 11 auf wenigen Rechnern. Nutzung auf eigenes Risiko, ohne Gewähr (siehe Lizenz).

## Installieren

1. Unter [Releases](../../releases) die neueste Datei **`TalkSetup-…-komplett.exe`** herunterladen (rund 570 MB, enthält die Sprachmodelle).
2. Doppelklicken. Windows zeigt wahrscheinlich **„Der Computer wurde durch Windows geschützt“** – das Setup ist nicht kostenpflichtig signiert. Auf **„Weitere Informationen“** und dann **„Trotzdem ausführen“** klicken.
3. Den Schritten folgen. Keine Administratorrechte nötig, Talk landet in `%LOCALAPPDATA%\Programs\Talk`.

Das Talk-Symbol steht danach rechts unten neben der Uhr (eventuell hinter dem kleinen Pfeil). Ein Klick darauf öffnet das Menü.

**Voraussetzungen:** Windows 11 64 Bit, Prozessor mit AVX2 (die meisten ab etwa 2015), Mikrofon, etwa 1 GB Platz. Eine Grafikkarte macht die lokale Erkennung deutlich schneller; ohne läuft sie auf dem Prozessor (wie schnell, ist nicht gemessen).

**Virenscanner:** Talk hört global auf die Caps-Lock-Taste. Manche Virenscanner stufen solche Programme vorsichtshalber als verdächtig ein. Talk wertet nur Caps Lock, Umschalt und Esc aus – der Quelltext liegt hier offen zum Nachprüfen.

**Prüfsumme:** Zu jedem Release steht die SHA-256-Prüfsumme in den Release-Notes. Prüfen in PowerShell: `Get-FileHash .\TalkSetup-…-komplett.exe`

## Bedienen

| Was | Wie |
|---|---|
| Aufnahme starten | Zweimal kurz auf Caps Lock tippen |
| Aufnahme beenden, Text einfügen | Einmal auf Caps Lock tippen |
| Abbrechen | Esc |
| Feststelltaste wie früher | Umschalt + Caps Lock |

Die ausführliche Anleitung (Wörterbuch, Glätten, Diagnose) liegt nach der Installation als `Liesmich.txt` im Programmordner und hier unter [`installer/Liesmich.txt`](installer/Liesmich.txt).

## Groq (optional)

Ohne Schlüssel erkennt Talk alles lokal. Mit einem **eigenen** Groq-Schlüssel geht es meist etwas schneller, und „Glätten“ sowie Mundart werden möglich.

1. Auf [console.groq.com](https://console.groq.com) ein Konto anlegen, unter „API Keys“ einen Schlüssel erstellen und kopieren.
2. In Talk: Einstellungen → Datenschutz → „Groq Schlüssel“ → „Aus Zwischenablage einfügen“ → „Speichern“.

Der Schlüssel liegt nur in der Windows-Anmeldeinformationsverwaltung, nie in einer Datei. Ob Groq kostenlos bleibt und welche Grenzen gelten, steht bei Groq.

## Datenschutz

- **Ohne Groq-Schlüssel oder mit „Nur lokal“** verlässt nichts deinen Rechner.
- **Mit Groq** gehen Aufnahme und Text an Groq, einen Dienst mit Sitz in den USA. Für Gesundheit, Kundendaten und andere sensible Inhalte „Nur lokal“ einschalten.
- Ohne erkannte Sprache wird nie etwas gesendet.
- Audio bleibt im Arbeitsspeicher und wird nie auf die Festplatte geschrieben.
- Der Verlauf ist mit deinem Windows-Konto verschlüsselt und wird nach 30 Tagen gelöscht.
- Das Protokoll unter `%LOCALAPPDATA%\Talk\logs` enthält keine diktierten Inhalte und keine Schlüssel.
- Wörter der Liste „Privat“ gehen nie an Groq.

## Selbst bauen

Für alle, die am Code mitbasteln wollen.

**Voraussetzungen:** Windows 11, [.NET SDK 10](https://dotnet.microsoft.com/download) (genaue Version in `global.json`), git.

```powershell
git clone <Adresse dieses Repositorys>
cd talk
dotnet build
dotnet test
dotnet run --project src/Talk.App
```

**Modelle:** Die App lädt keine Modelle selbst herunter. Für die lokale Erkennung gehören zwei Dateien nach `%LOCALAPPDATA%\Talk\models`:

- `ggml-large-v3-turbo-q5_0.bin` (Whisper, aus dem Hugging-Face-Repository `ggerganov/whisper.cpp`)
- `ggml-silero-v6.2.0.bin` (Stilleerkennung, aus dem Hugging-Face-Repository `ggml-org/whisper-vad`)

Ein anderer Modellordner geht über `modelsDirectory` in `%LOCALAPPDATA%\Talk\settings.json`.

**Setup bauen** (braucht [Inno Setup](https://jrsoftware.org/isinfo.php) ab 6.3):

```powershell
pwsh -File installer\baue-setup.ps1 -NurOrdner   # nur Programmordner
pwsh -File installer\baue-setup.ps1              # Setup ohne Modelle
pwsh -File installer\baue-setup.ps1 -Komplett    # Setup mit Modellen und VC-Laufzeit
```

Ausgabe landet in `ausgabe\`. Für `-Komplett` die Modelle per `-Modelle <Ordner>` und die VC++-Laufzeit per `-VcRedist <Datei>` angeben.

**Diagnose-Schalter** für Entwickler: `--selftest`, `--hooktest <s>`, `--audiotest <s>`, `--stttest <wav> [--local]`, `--keycheck`, `--logcheck` u. a. – siehe `src/Talk.App/App.xaml.cs`.

### Aufbau

| Pfad | Inhalt |
|---|---|
| `src/Talk.Core` | Reine Logik (net10.0): Hotkey-Regeln, Nachbearbeitung, Wörterbuch, Glättung, Einfügeregeln |
| `src/Talk.App` | Windows-App (WPF, Tray-Icon), Aufnahme, Erkennung über Groq und Whisper.net |
| `tests/Talk.Core.Tests` | xUnit-Tests |
| `installer/` | Inno-Setup-Skript, Bauskripte, Kurzanleitung, Lizenzen Dritter |
| `assets/symbol/` | Symbole und ihre Erzeugung |
| `DESIGN.md` | Gestaltungsregeln der Oberfläche |

Kommentare im Code verweisen teils auf Abschnitte eines internen Projektbuchs, das nicht veröffentlicht ist. Die Regeln selbst stehen jeweils in den Tests.

## Mitmachen und Fehler melden

Fehler und Ideen gern als Issue. Bitte **nie einen API-Schlüssel, Diktat-Inhalte oder private Namen** in ein Issue schreiben – ein Foto des Diagnose-Fensters reicht meist. Antworten kommen, wenn Zeit ist; es gibt keinen garantierten Support. Mehr in [SECURITY.md](SECURITY.md).

## Lizenz

[MIT](LICENSE) © 2026 Stefano Cesaretti. Lizenzen der verwendeten Bibliotheken, Schriften und Modelle stehen in [`installer/Lizenzen/`](installer/Lizenzen/).
