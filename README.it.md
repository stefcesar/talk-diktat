[🇩🇪 Deutsch](README.md) · [🇬🇧 English](README.en.md) · **🇮🇹 Italiano**

> 🇮🇹 **Stai leggendo la versione italiana.** Dalla versione 0.5.0 Talk stesso è disponibile in italiano: in «Impostazioni», sotto «Lingua dell'interfaccia» scegli «Italiano».

# Talk: dettare in qualsiasi programma su Windows

<sub>Created by **Stefano Cesaretti** · realizzato con Claude Code</sub>

Talk funziona in modo invisibile in background. Tocca due volte **Bloc Maiusc** (Caps Lock), parla, tocca ancora una volta, e il testo appare dove stai scrivendo: in Word, nel browser, in WhatsApp, in qualsiasi campo di testo.

- Riconosce la voce **sul tuo computer**, senza internet (Whisper, sulla scheda grafica se presente)
- In opzione più veloce tramite **Groq** con una tua chiave, in più la "rifinitura" del testo
- Interruttore **"Nur lokal"** (solo locale): così nulla lascia mai il tuo computer
- Un tuo **dizionario** per nomi, marchi e termini tecnici
- Tedesco, italiano, inglese, svizzero tedesco e dialetto svizzero
- **Si usa con un tasto alla volta**: nessuna combinazione di tasti, grandi superfici da cliccare, funziona bene con la tastiera su schermo

> **Stato: beta.** Testato solo su Windows 11 su pochi computer. Uso a proprio rischio, senza garanzia (vedi licenza).

## Installare

1. Nella pagina [Releases](../../releases) scarica il file più recente **`TalkSetup-…-komplett.exe`** (circa 570 MB, contiene i modelli vocali). Accanto c'è la **guida breve in PDF**.
2. Fai doppio clic. Windows mostrerà probabilmente **"Windows ha protetto il PC"**, perché il setup non ha una firma digitale (che è a pagamento). Clicca su **"Ulteriori informazioni"** e poi su **"Esegui comunque"**.
3. Segui i passaggi. Non servono diritti di amministratore; Talk viene installato in `%LOCALAPPDATA%\Programs\Talk`.

L'icona di Talk appare poi in basso a destra accanto all'orologio (eventualmente dietro la piccola freccia). Un clic apre il menu.

**Requisiti:** Windows 11 a 64 bit, un processore con AVX2 (la maggior parte dal 2015 circa), un microfono, circa 1 GB di spazio libero. Una scheda grafica rende il riconoscimento locale molto più veloce; senza, funziona sul processore (quanto velocemente non è stato misurato).

**Antivirus:** Talk ascolta il tasto Bloc Maiusc in tutto il sistema. Alcuni antivirus considerano questi programmi sospetti per precauzione. Talk valuta solo Bloc Maiusc, Maiusc ed Esc; il codice sorgente è qui, aperto da verificare.

**Checksum:** Il checksum SHA-256 di ogni release è nelle note di rilascio. Verifica in PowerShell: `Get-FileHash .\TalkSetup-…-komplett.exe`

## Usare

| Cosa | Come |
|---|---|
| Avviare la registrazione | Toccare due volte velocemente Bloc Maiusc |
| Fermare, inserire il testo | Toccare una volta Bloc Maiusc |
| Annullare | Esc |
| Bloc Maiusc come prima | Maiusc + Bloc Maiusc |

**Guida con immagini:** [Talk-Guida-IT.pdf](https://github.com/stefcesar/talk-diktat/releases/download/v0.5.1/Talk-Guida-IT.pdf) (7 pagine). La stessa guida come testo semplice: [`installer/Liesmich.it.txt`](installer/Liesmich.it.txt).

## Con o senza Groq

Talk riconosce la voce sul tuo computer, senza internet. Groq è un servizio in internet che può farlo al suo posto.

| | Senza Groq (così parte Talk) | Con Groq |
|---|---|---|
| Internet | non serve | serve |
| Account | nessuno | account presso Groq, inserire una chiave una volta |
| I tuoi dati | restano sul tuo computer | registrazione e testo vanno a Groq (USA) |
| Velocità | il primo dettato dopo l'avvio richiede più tempo | di solito un po' più veloce, soprattutto il primo dettato |
| Extra | nessuno | • Rifinitura: testo riformulato in modo pulito<br>• Dialetto svizzero |

**Regola pratica:** inizia senza Groq. Se Talk è troppo lento per te o vuoi la rifinitura, collega Groq. Se Groq non funziona, Talk continua da solo a riconoscere sul computer.

## Groq (facoltativo)

Senza chiave, Talk riconosce tutto in locale. Con una chiave Groq **tua** di solito è un po' più veloce, e diventano possibili la rifinitura del testo e il dialetto svizzero.

1. Crea un account su [console.groq.com](https://console.groq.com), sotto "API Keys" crea una chiave e copiala.
2. In Talk: «Impostazioni» → «Privacy» → «Chiave Groq» → «Incolla dagli Appunti» → «Salva».

La chiave viene salvata solo in Gestione credenziali di Windows, mai in un file. Se Groq resta gratuito e quali limiti valgono, lo indica Groq.

## Privacy

- **Senza chiave Groq o con "Nur lokal"** nulla lascia il tuo computer.
- **Con Groq** registrazione e testo vanno a Groq, un servizio con sede negli USA. Per dati sanitari, dati di clienti e altri contenuti sensibili attiva "Nur lokal".
- Se non viene riconosciuta alcuna voce, non viene mai inviato nulla.
- L'audio resta nella memoria di lavoro e non viene mai scritto sul disco.
- La cronologia è cifrata con il tuo account Windows e viene cancellata dopo 30 giorni.
- Il registro in `%LOCALAPPDATA%\Talk\logs` non contiene testi dettati né chiavi.
- Le parole della lista "Privat" (privato) non vanno mai a Groq.

## Domande frequenti

**Talk costa qualcosa?**
No. Talk è gratuito e open source (licenza MIT). Solo chi usa Groq ha bisogno di un proprio account lì; se Groq resta gratuito e quanto si può usare al giorno, lo indica Groq.

**Durante l'installazione Windows avvisa: "Windows ha protetto il PC". È pericoloso?**
L'avviso compare perché il setup non ha una firma digitale, non perché sia stato trovato qualcosa. Clicca su "Ulteriori informazioni" e poi su "Esegui comunque". Per maggiore sicurezza confronta prima il checksum SHA-256 delle note di rilascio (vedi "Installare").

**Il mio antivirus segnala Talk. Perché?**
Talk ascolta il tasto Bloc Maiusc in tutto il sistema. Alcuni antivirus considerano sospetti per precauzione i programmi che ascoltano i tasti. Talk valuta solo Bloc Maiusc, Maiusc ed Esc; il codice sorgente è qui, aperto da verificare.

**Serve internet?**
No. Senza chiave Groq, Talk riconosce tutto sul tuo computer. Internet serve solo per Groq, la rifinitura e il dialetto svizzero.

**Talk funziona su Mac, Linux o Windows 10?**
No, per ora solo su Windows 11 (64 bit), testato su pochi computer.

**Talk esiste in italiano?**
Sì, dalla versione 0.5.0. In «Impostazioni», sotto «Lingua dell'interfaccia» scegli «Italiano». Talk riconosce anche l'italiano parlato.

**Non riesco a premere due tasti contemporaneamente. Posso usare Talk lo stesso?**
Sì. Si detta con un solo tasto: toccare due volte velocemente Bloc Maiusc, parlare, toccare una volta. Funziona anche con la tastiera su schermo. Se il doppio tocco è troppo veloce per te, nelle impostazioni imposta più tempo sotto «Tempo tra i due tocchi su Bloc Maiusc». Tutto il resto si fa con singoli clic tramite l'icona di Talk accanto all'orologio.

**Bloc Maiusc non avvia nulla.**
Apri «Diagnostica» nel menu: se "Start" non aumenta quando tocchi, tocca due volte più velocemente o imposta più tempo tra i tocchi nelle impostazioni. Altrimenti chiudi Talk e riavvialo dal menu Start.

**La registrazione parte, ma non arriva nessun testo.**
Nella diagnostica controlla se il livello si muove quando parli. Se no, nelle impostazioni scegli un altro microfono sotto «Microfono» o clicca su «Ricarica».

**Talk scrive sempre male un nome.**
Nella cronologia clicca su «Correggi parola» accanto al testo, oppure aggiungi la parola nelle impostazioni sotto «Dizionario». Dal dettato successivo è scritta correttamente.

**Come aggiorno Talk?**
Chiudi Talk dal menu ed esegui il nuovo setup sopra l'installazione esistente.

**Come rimuovo Talk?**
Disinstallalo nelle impostazioni di Windows sotto "App". Impostazioni, cronologia, liste di parole e modelli in `%LOCALAPPDATA%\Talk` restano; per cancellare tutto, elimina questa cartella a mano.

**Dove trovo aiuto?**
Qui come [issue](../../issues), in italiano, tedesco o inglese. Per favore non scrivere mai una chiave API, testi dettati o nomi privati; di solito basta una foto della finestra di diagnostica.

## Compilarlo da sé

Per chi vuole mettere le mani nel codice.

**Requisiti:** Windows 11, [.NET SDK 10](https://dotnet.microsoft.com/download) (versione esatta in `global.json`), git.

```powershell
git clone <indirizzo di questo repository>
cd talk
dotnet build
dotnet test
dotnet run --project src/Talk.App
```

**Modelli:** Il programma non scarica modelli da solo. Per il riconoscimento locale servono due file in `%LOCALAPPDATA%\Talk\models`:

- `ggml-large-v3-turbo-q5_0.bin` (Whisper, dal repository Hugging Face `ggerganov/whisper.cpp`)
- `ggml-silero-v6.2.0.bin` (rilevamento del silenzio, dal repository Hugging Face `ggml-org/whisper-vad`)

Un'altra cartella dei modelli si imposta con `modelsDirectory` in `%LOCALAPPDATA%\Talk\settings.json`.

**Compilare il setup** (serve [Inno Setup](https://jrsoftware.org/isinfo.php) dalla versione 6.3):

```powershell
pwsh -File installer\baue-setup.ps1 -NurOrdner   # solo cartella del programma
pwsh -File installer\baue-setup.ps1              # setup senza modelli
pwsh -File installer\baue-setup.ps1 -Komplett    # setup con modelli e runtime VC
```

Il risultato finisce in `ausgabe\`. Per `-Komplett` indica i modelli con `-Modelle <cartella>` e il runtime VC++ con `-VcRedist <file>`.

**Opzioni di diagnostica** per sviluppatori: `--selftest`, `--hooktest <s>`, `--audiotest <s>`, `--stttest <wav> [--local]`, `--keycheck`, `--logcheck` e altre; vedi `src/Talk.App/App.xaml.cs`.

### Struttura

| Percorso | Contenuto |
|---|---|
| `src/Talk.Core` | Logica pura (net10.0): regole del tasto, rielaborazione, dizionario, rifinitura, regole di inserimento |
| `src/Talk.App` | App Windows (WPF, icona nell'area di notifica), registrazione, riconoscimento tramite Groq e Whisper.net |
| `tests/Talk.Core.Tests` | Test xUnit |
| `installer/` | Script Inno Setup, script di compilazione, guida breve, licenze di terzi |
| `assets/symbol/` | Icone e come vengono generate |
| `DESIGN.md` | Regole grafiche dell'interfaccia |

Commenti e nomi nel codice sono in tedesco. Alcuni commenti rimandano a sezioni di un documento di progetto interno non pubblicato; le regole stesse sono nei test.

## Partecipare e segnalare errori

Errori e idee sono benvenuti come issue. Per favore **non scrivere mai una chiave API, testi dettati o nomi privati** in una issue; di solito basta una foto della finestra di diagnostica. Le risposte arrivano quando c'è tempo; non c'è assistenza garantita. Altro in [SECURITY.md](SECURITY.md).

## Licenza

[MIT](LICENSE) © 2026 Stefano Cesaretti. Le licenze delle librerie, dei font e dei modelli usati sono in [`installer/Lizenzen/`](installer/Lizenzen/).
