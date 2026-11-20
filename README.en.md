[🇩🇪 Deutsch](README.md) · **🇬🇧 English** · [🇮🇹 Italiano](README.it.md)

> 🇬🇧 **You are reading the English version.** The app's user interface and the PDF guide are currently in German only; German labels are quoted below with their meaning in brackets.

# Talk: dictate into any program on Windows

<sub>Created by **Stefano Cesaretti** · built with Claude Code</sub>

Talk runs invisibly in the background. Double-tap **Caps Lock**, speak, tap once more, and the text appears wherever you are typing: in Word, in the browser, in WhatsApp, in any text field.

- Recognizes speech **on your computer**, no internet needed (Whisper, on the graphics card if available)
- Optionally faster via **Groq** with your own key, plus "smoothing" (cleaning up the text)
- **"Nur lokal"** (local only) switch: then nothing ever leaves your computer
- Your own **dictionary** for names, brands and technical terms
- German, Italian, English, Swiss German and Swiss dialect
- **Operable with one key at a time**: no key combinations, large click targets, works well with an on-screen keyboard

> **Status: beta.** Tested only on Windows 11 on a few computers. Use at your own risk, without warranty (see license).

## Install

1. Under [Releases](../../releases), download the latest **`TalkSetup-…-komplett.exe`** (about 570 MB, includes the speech models). The **short guide as PDF** (German) is next to it.
2. Double-click it. Windows will probably show **"Windows protected your PC"** because the setup is not code-signed (signing costs money). Click **"More info"**, then **"Run anyway"**.
3. Follow the steps. No administrator rights needed; Talk is installed to `%LOCALAPPDATA%\Programs\Talk`.

The Talk icon then appears at the bottom right next to the clock (possibly behind the small arrow). One click opens the menu.

**Requirements:** Windows 11 64-bit, a processor with AVX2 (most from about 2015 on), a microphone, about 1 GB of free space. A graphics card makes local recognition much faster; without one it runs on the processor (how fast has not been measured).

**Antivirus:** Talk listens globally to the Caps Lock key. Some antivirus programs flag such programs as suspicious as a precaution. Talk only evaluates Caps Lock, Shift and Esc; the source code is here to check.

**Checksum:** The SHA-256 checksum of each release is in the release notes. Check it in PowerShell: `Get-FileHash .\TalkSetup-…-komplett.exe`

## Use

| What | How |
|---|---|
| Start recording | Tap Caps Lock twice quickly |
| Stop recording, insert text | Tap Caps Lock once |
| Cancel | Esc |
| Normal Caps Lock | Shift + Caps Lock |

**Guide with pictures (German):** [Talk-Kurzanleitung.pdf](https://github.com/stefcesar/talk-diktat/releases/download/v0.4.1/Talk-Kurzanleitung.pdf). The same guide as plain text: [`installer/Liesmich.txt`](installer/Liesmich.txt).

## Groq (optional)

Without a key, Talk recognizes everything locally. With your **own** Groq key it is usually a little faster, and "Glätten" (smoothing) and Swiss dialect output become possible.

1. Create an account at [console.groq.com](https://console.groq.com), create a key under "API Keys" and copy it.
2. In Talk: „Einstellungen“ (Settings) → „Datenschutz“ (Privacy) → „Groq Schlüssel“ (Groq key) → „Aus Zwischenablage einfügen“ (paste from clipboard) → „Speichern“ (save).

The key is stored only in the Windows Credential Manager, never in a file. Whether Groq stays free and which limits apply is stated by Groq.

## Privacy

- **Without a Groq key or with "Nur lokal"** nothing leaves your computer.
- **With Groq**, the recording and text go to Groq, a service based in the USA. For health data, customer data and other sensitive content, switch on "Nur lokal".
- If no speech is detected, nothing is ever sent.
- Audio stays in memory and is never written to disk.
- The history is encrypted with your Windows account and deleted after 30 days.
- The log in `%LOCALAPPDATA%\Talk\logs` contains no dictated content and no keys.
- Words in the "Privat" (private) list never go to Groq.

## Frequently asked questions

**Does Talk cost anything?**
No. Talk is free and open source (MIT license). Only if you use Groq do you need your own account there; whether Groq stays free and how much you can use per day is stated by Groq.

**Windows warns during installation: "Windows protected your PC". Is that dangerous?**
The warning appears because the setup is not code-signed, not because anything was found. Click "More info", then "Run anyway". To be sure, compare the SHA-256 checksum from the release notes first (see "Install").

**My antivirus flags Talk. Why?**
Talk listens globally to the Caps Lock key. Some antivirus programs flag programs that listen to keys as suspicious as a precaution. Talk only evaluates Caps Lock, Shift and Esc; the source code is here to check.

**Do I need internet?**
No. Without a Groq key, Talk recognizes everything on your computer. You only need internet for Groq, smoothing and Swiss dialect output.

**Does Talk run on Mac, Linux or Windows 10?**
No, so far only on Windows 11 (64-bit), tested on a few computers.

**Is Talk available in English?**
Talk can recognize English speech. The user interface (menu, settings, guide) is currently in German only.

**I can't press two keys at the same time. Can I still use Talk?**
Yes. Dictation works with a single key: tap Caps Lock twice quickly, speak, tap once. This also works with the on-screen keyboard. If the double tap is too fast for you, set more time under „Zeit zwischen den beiden Tipps“ (time between the two taps) in the settings. Everything else works with single clicks via the Talk icon next to the clock.

**Caps Lock doesn't start anything.**
Open „Diagnose“ (diagnostics) in the menu: if "Start" does not count up when you tap, tap twice faster or set more time between the taps in the settings. Otherwise quit Talk and start it again from the Start menu.

**Recording runs, but no text appears.**
In the diagnostics, check whether the level moves when you speak. If not, choose a different microphone under „Mikrofon“ in the settings or click „Neu laden“ (reload).

**Talk always misspells a name.**
In the history, click „Wort korrigieren“ (correct word) next to the text, or add the word under „Wörterbuch“ (dictionary) in the settings. From the next dictation on it is spelled correctly.

**How do I update Talk?**
Quit Talk from the menu and run the new setup over the old installation.

**How do I remove Talk?**
Uninstall it in the Windows settings under "Apps". Settings, history, word lists and models in `%LOCALAPPDATA%\Talk` are kept; to delete everything, delete this folder by hand.

**Where can I get help?**
Here as an [issue](../../issues), in English or German. Please never post an API key, dictated content or private names; a photo of the diagnostics window is usually enough.

## Build it yourself

For everyone who wants to tinker with the code.

**Requirements:** Windows 11, [.NET SDK 10](https://dotnet.microsoft.com/download) (exact version in `global.json`), git.

```powershell
git clone <address of this repository>
cd talk
dotnet build
dotnet test
dotnet run --project src/Talk.App
```

**Models:** The app does not download any models by itself. For local recognition, two files belong in `%LOCALAPPDATA%\Talk\models`:

- `ggml-large-v3-turbo-q5_0.bin` (Whisper, from the Hugging Face repository `ggerganov/whisper.cpp`)
- `ggml-silero-v6.2.0.bin` (silence detection, from the Hugging Face repository `ggml-org/whisper-vad`)

A different model folder can be set via `modelsDirectory` in `%LOCALAPPDATA%\Talk\settings.json`.

**Build the setup** (needs [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3 or later):

```powershell
pwsh -File installer\baue-setup.ps1 -NurOrdner   # program folder only
pwsh -File installer\baue-setup.ps1              # setup without models
pwsh -File installer\baue-setup.ps1 -Komplett    # setup with models and VC runtime
```

Output goes to `ausgabe\`. For `-Komplett`, pass the models with `-Modelle <folder>` and the VC++ runtime with `-VcRedist <file>`.

**Diagnostic switches** for developers: `--selftest`, `--hooktest <s>`, `--audiotest <s>`, `--stttest <wav> [--local]`, `--keycheck`, `--logcheck` and more; see `src/Talk.App/App.xaml.cs`.

### Structure

| Path | Content |
|---|---|
| `src/Talk.Core` | Pure logic (net10.0): hotkey rules, post-processing, dictionary, smoothing, insert rules |
| `src/Talk.App` | Windows app (WPF, tray icon), recording, recognition via Groq and Whisper.net |
| `tests/Talk.Core.Tests` | xUnit tests |
| `installer/` | Inno Setup script, build scripts, short guide, third-party licenses |
| `assets/symbol/` | Icons and how they are generated |
| `DESIGN.md` | Design rules for the user interface |

Code comments and identifiers are in German. Some comments refer to sections of an internal project book that is not published; the rules themselves are in the tests.

## Contribute and report bugs

Bugs and ideas are welcome as issues. Please **never put an API key, dictated content or private names** in an issue; a photo of the diagnostics window is usually enough. Answers come when there is time; there is no guaranteed support. More in [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 Stefano Cesaretti. Licenses of the libraries, fonts and models used are in [`installer/Lizenzen/`](installer/Lizenzen/).
