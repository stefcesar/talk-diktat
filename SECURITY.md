# Sicherheit

## Schwachstelle gefunden?

Bitte **kein öffentliches Issue** öffnen, sondern GitHub „Security → Report a vulnerability“ (private Meldung) nutzen. Eine Antwort kommt, sobald Zeit ist; Talk ist ein privates Freizeitprojekt ohne garantierten Support.

## Was nie in ein Issue gehört

- API-Schlüssel (Groq oder andere). Ist einer aus Versehen öffentlich geworden: sofort auf console.groq.com löschen und neu erstellen.
- Diktierte Texte, Verlauf, Inhalte des Wörterbuchs
- Namen von Menschen, Gesundheitsdaten

Ein Foto oder Screenshot des Fensters „Diagnose“ (Tray-Menü) reicht für die meisten Fehler.

## Was Talk zum Schutz tut

- Der Groq-Schlüssel liegt nur in der Windows-Anmeldeinformationsverwaltung, nie in Dateien oder im Protokoll.
- Audio bleibt im Arbeitsspeicher und wird nach Gebrauch überschrieben.
- Der Tastatur-Hook wertet nur Caps Lock, Umschalt und Esc aus.
- Talk fügt nie in Passwortfelder und nie in Fenster mit höheren Rechten ein.
