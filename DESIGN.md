# DESIGN.md: Talk

Stand: 01.11.2026. Diese Datei ist Gesetz für jedes Fenster. Eine Entscheidung hier gilt überall und wird nie pro Fenster neu geraten.

Richtung: eigene Handschrift, abgeleitet aus dem Overlay (dunkle Pille, Talk Zeichen, Orange). Grundlage ist die Richtung "Soft Dark" aus dem Skill `frontend-ohne-slop`.

## Gefühl

Stimmungswörter: **ruhig, griffig, eigen, verlässlich.**
Streitfälle entscheidet die Frage: Wirkt es ruhig, und trifft man es auch mit einer Bildschirmtastatur oder einem Kopf- oder Mundsteuergerät beim ersten Klick?

Talk ist eine Werkstatt, keine Bühne. Es gibt keine Deko, keine Bilder im Arbeitsbereich, nur Schrift, Fläche und den einen Akzent.

## Farben

Nur dunkel. Talk folgt nicht dem hellen Windows Modus, damit Overlay, Menü und Fenster eine Familie sind. Die Titelleiste wird über die Windows Schnittstelle dunkel geschaltet, die Knöpfe oben rechts bleiben die von Windows.

| Name | Wert | Wofür |
|---|---|---|
| `Bg` | `#15171B` | Grund jedes Fensters, nie reines Schwarz |
| `Surface` | `#1D2025` | Gruppen, Menü, Eingabefelder |
| `SurfaceHover` | `#262A30` | Fläche unter der Maus, gedrückte Knöpfe |
| `Border` | `#2E3238` | Linien, 1 px |
| `Text` | `#ECEBE7` | Tinte |
| `TextMuted` | `#A3A19B` | Erklärungen, Zeitstempel (Kontrast 6,4 zu 1) |
| `Accent` | `#FF6A3D` | Talk Orange aus dem Overlay |
| `OnAccent` | `#15171B` | Schrift auf dem Akzent (weiß auf Orange ist zu schwach, Kontrast 2,9 zu 1) |
| `Warn` | `#E8B44C` | Hinweise, die Handeln brauchen (Mikrofon fehlt, Autostart gescheitert) |
| `Danger` | `#F47174` | nur "Alles löschen" und "Schlüssel löschen" (Kontrast 5,9 zu 1 auf `Surface`; der erste Wert `#E5484D` lag mit 3,5 zu 1 zu tief) |
| `Success` | `#5BB98B` | nur Bestätigungen ("Schlüssel geprüft") |

Regeln:
1. Der Akzent hat genau einen Job: die Hauptaktion eines Fensters und aktive Zustände (Schalter an, gewählter Eintrag, Pegel im Overlay). Er ist nie Fläche, nie Fließtext, nie Deko.
2. Pro Fenster höchstens ein Knopf in Orange.
3. Die bisherigen Farben "Gray", "DarkOrange" und `#DDDDDD` verschwinden aus dem Code. Jede Farbe kommt aus einer Ressource in `App.xaml`.

## Schrift

Die Schriften werden als Dateien mit der App ausgeliefert (Ressource im Programm), nie aus dem Netz geladen. Alle drei stehen unter der freien SIL Open Font License.

| Rolle | Schrift | Gewicht | Wo |
|---|---|---|---|
| Titel | Bricolage Grotesque | 800 | Fenstertitel im Inhalt, große leere Zustände |
| Text | Public Sans | 400, Labels 600 | alles andere |
| Zahlen | Geist Mono | 400 | Millisekunden, Uhrzeiten im Verlauf, Diagnosewerte |

Dateien: `src\Talk.App\Ui\Fonts` (statische TrueType Schnitte von Google Fonts, geladen am 01.11.2026, Lizenzen daneben als `OFL-*.txt`). Alle Umlaute, ß, „“, € und italienische Akzente sind enthalten. Das Zeichen ⇧ fehlt in Public Sans; WPF nimmt dafür von selbst eine Windows Schrift. Windows nennt die Titelschrift intern "Bricolage Grotesque 14pt ExtraBold"; beim Einbinden prüfen, unter welchem Namen WPF sie findet.

Verboten als Identität: Segoe UI (Standard von WPF), Inter, Roboto, Arial.

Skala in Punkten: 12 / 14 / 16 / 20 / 32.
1. 32 nur für den Titel oben im Fenster ("Einstellungen", "Verlauf").
2. 16 für Abschnittstitel, Gewicht 600, in `Text`, nicht grau. Die Abschnitte sind damit klar zu sehen.
3. 14 für Bedienelemente und Text, 12 nur für Erklärungen in `TextMuted`.

## Fläche und Tiefe

1. Abstände im Raster von 8: 8, 16, 24, 32. Fensterrand innen 24.
2. **Ein Radius: 8.** Einzige Ausnahme ist die Pille des Overlays (ganz rund), sie ist das Markenzeichen.
3. Tiefe: flach mit 1 px Linien in `Border`. Keine Schatten, außer dem einen, den Windows dem Fenster selbst gibt.
4. Höchstens zwei Ebenen: Fenster, darin Gruppen. Keine Karte in der Karte.
5. **Klickflächen mindestens 40 hoch**, Schalter und Knöpfe mit großzügiger Trefferfläche, auch das Wort neben dem Schalter löst ihn aus. Nichts erscheint nur beim Darüberfahren. Keine Tastenkombinationen nötig (beKEY).

## Bedienelemente

1. **Knopf, Hauptaktion:** Fläche `Accent`, Schrift `OnAccent` 600, Radius 8, Höhe 40.
2. **Knopf, normal:** Fläche `Surface`, Linie `Border`, Schrift `Text`. Unter der Maus `SurfaceHover`.
3. **Knopf, gefährlich:** wie normal, Schrift `Danger`. Fragt immer nach.
4. **Schalter statt Häkchen** für An und Aus: Schiene 40 mal 22. An: Schiene `Accent`, Knopf `OnAccent`. Aus: Schiene `Surface` mit Rand und Knopf in `TextMuted`, damit der Schalter auf dem dunklen Grund sichtbar bleibt. Text links, Schalter rechts, die ganze Zeile ist klickbar.
5. **Auswahl** (Mikrofon, Sprache, Stufe): eigene Vorlage in `Surface` mit Linie, Liste in `Surface`, gewählter Eintrag mit Akzent als Schriftfarbe.
6. **Hinweise** stehen unter dem Element in 12 Punkt, `TextMuted`. Nur was Handeln braucht, steht in `Warn`.
7. **Tray Menü:** eigener Zeichner für das Windows Menü (`src\Talk.App\Ui\TrayMenu.cs`): Grund `Surface`, Linie `Border`, Einträge mindestens 40 hoch, Haken bei "nur lokal" und "Glätten" in `Accent`, "Talk beenden" in `TextMuted`, runde Ecken von Windows 11. Fläche unter der Maus `MenuHover` `#33383F` (nur hier; `SurfaceHover` war im Menü zu schwach zu sehen, Nachtrag 01.11.2026).
8. **Fokusring** nur bei Bedienung mit der Tastatur, 1 px `TextMuted`, nie nach einem Mausklick.
9. **Bildlaufleiste** 16 breit, Griff `#4A4F57`, unter der Maus `TextMuted`.

Umsetzung: alles in `src\Talk.App\Ui\Theme.xaml`, jedes Fenster ruft nach `InitializeComponent` `WindowTheme.Apply(this)` auf. Bilder ohne laufendes Talk: `Talk.exe --uishots Ordner` (Musterbogen, Verlauf gefüllt, leer und übervoll, Schlüsseldialog; erfundene Texte, Exit 0).

## Bewegung

1. Rückmeldung auf Klick und Maus in 150 ms (Farbwechsel, kein Vergrößern).
2. Das Overlay behält seine Pegelbalken und das Pulsieren, sonst bewegt sich nichts.
3. Ist in Windows "Animationen anzeigen" aus, entfällt jede Bewegung außer dem Pegel.

## Symbole und Bilder

1. Einziges Bildzeichen ist das Talk Zeichen aus `assets\symbol`. Keine weiteren Symbole, keine Emojis.
2. Leere Zustände werden mit Schrift gelöst: ein großer Satz in Bricolage, ein Satz Erklärung, falls sinnvoll ein Knopf.

## Verboten

Verläufe in Lila oder Türkis · Emojis als Symbole · blinkende Punkte · farbige Balken als Deko · gemischte Symbolsätze · Floskeltexte · Karten in Karten · mehr als ein Orange pro Fenster · weiße Fensterflächen.

## Reihenfolge des Umbaus

Ein Fenster pro Runde, jeweils mit Screenshot, Abgleich gegen diese Datei, den drei größten Abweichungen behoben, dann Halt für eine Freigabe.

1. Grundlage: Farben, Schriften und Vorlagen der Bedienelemente in `App.xaml`, dunkle Titelleiste.
2. Tray Menü.
3. Verlauf.
4. Einstellungen (dabei: Abschnitte gliedern, Doppeltipp als Wahl statt vier Elementen, "Schließen" entfällt oder wird ruhig).
5. Schlüsseldialog.
6. Overlay: nur Feinschliff an Schrift und Farben.
7. Diagnosefenster: nur die Grundlage übernehmen, kein eigener Umbau.

Stand 01.11.2026: alle sieben Runden gebaut und freigegeben.

Jedes Fenster wird in seinen Zuständen geprüft: leer, gefüllt, übervoll (sehr lange Texte, viele Einträge), Fehler.
