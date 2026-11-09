// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.Net.Http aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.Net.Http;

using Talk.App.Diagnostics;
using Talk.App.Net;
using Talk.App.Secrets;
using Talk.Core.Audio;
using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Secrets;
using Talk.Core.Transcription;

namespace Talk.App.Transcription;

/// <summary>Zustand des lokalen Modells nach dem Vorladen.</summary>
internal enum LocalModelState
{
    /// <summary>Es gibt keinen lokalen Erkenner, oder das Vorladen wurde beim Beenden abgebrochen.</summary>
    None,

    /// <summary>Das Modell ist geladen.</summary>
    Loaded,

    /// <summary>Die Modelldatei fehlt. Nachgeladen wird nie.</summary>
    Missing,

    /// <summary>Die Modelldatei ist da, lässt sich aber nicht laden.</summary>
    Failed,
}

/// <summary>
/// Verdrahtung der Erkennung, eine Stelle für Testfenster und Befehlszeile: Schlüsselablage, gemeinsamer HTTP Client,
/// Groq Erkenner, lokaler Erkenner als zweiter und die Weiche dazwischen.
/// </summary>
/// <remarks>
/// <para>
/// Alles, was ins Netz geht, läuft über diese Klasse: Erkennung, vorab aufgebaute Verbindung und Schlüsselprüfung.
/// Ist <see cref="LocalOnly"/> gesetzt, sendet keine der drei etwas. Ziel ist immer nur <c>api.groq.com</c> (siehe <see cref="TalkHttp"/>).
/// Der lokale Erkenner sendet nie etwas.
/// </para>
/// <para>
/// Der Schlüssel wird je Anfrage frisch aus der Ablage gelesen und hier nie gehalten, angezeigt oder protokolliert.
/// Erkannter Text wird in dieser Klasse nie protokolliert.
/// </para>
/// <para>
/// Der zweite Erkenner (lokal) wird an genau einer Stelle eingesetzt: in <see cref="CreateDefault"/>. Sein Modell lädt beim ersten
/// lokalen Lauf oder vorab über <see cref="PreloadLocalAsync"/>; danach steht einmal <c>stt | local | geladen ms=N laufzeit=...</c>
/// im Protokoll.
/// </para>
/// </remarks>
internal sealed class SpeechService : IDisposable
{
    private readonly ISecretStore _secrets;
    private readonly HttpClient _http;
    private readonly GroqTranscriber _groq;
    private readonly GroqSmoother _smoother;
    private readonly ITranscriber? _secondary;

    // Derselbe zweite Erkenner, wenn er der lokale ist; sonst null. Nur für Vorladen und Ladezeit.
    private readonly LocalWhisperTranscriber? _local;
    private readonly FallbackTranscriber _fallback;

    // Dieselbe Weiche mit fest eingeschaltetem "nur lokal" (Regel 1 der Weiche): Der erste Erkenner wird nie gerufen, es geht nichts ins Netz.
    // Für Clips ohne Sprachbefund, die nie an Groq gehen dürfen, unabhängig vom Schalter LocalOnly.
    private readonly FallbackTranscriber _localRoute;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;

    private volatile bool _localOnly;
    private int _localMissingLogged;
    private int _disposed;

    /// <summary>
    /// Steckt die Erkennung zusammen.
    /// </summary>
    /// <param name="secrets">Die Schlüsselablage.</param>
    /// <param name="http">Der gemeinsame HTTP Client der App. Er gehört nicht dieser Klasse und wird von ihr nicht freigegeben.</param>
    /// <param name="secondary">
    /// Der zweite Erkenner (lokal) oder null, solange es keinen gibt. Ist er <see cref="IDisposable"/>, gibt <see cref="Dispose"/> ihn frei.
    /// </param>
    public SpeechService(ISecretStore secrets, HttpClient http, ITranscriber? secondary = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secondary = secondary;
        _local = secondary as LocalWhisperTranscriber;
        _lifetimeToken = _lifetime.Token;

        _groq = new GroqTranscriber(_http, ReadGroqKey);
        _smoother = new GroqSmoother(_http, ReadGroqKey);
        _fallback = new FallbackTranscriber(_groq, _secondary, () => _localOnly);
        _localRoute = new FallbackTranscriber(_groq, _secondary, static () => true);
    }

    /// <summary>Die Schlüsselablage, zum Beispiel für den Schlüsseldialog.</summary>
    public ISecretStore Secrets => _secrets;

    /// <summary>
    /// True: Es wird nur lokal erkannt und nichts geht ins Internet (keine Erkennung über Groq, kein Verbindungsaufbau, keine Schlüsselprüfung).
    /// </summary>
    public bool LocalOnly
    {
        get => _localOnly;
        set => _localOnly = value;
    }

    /// <summary>
    /// Die eingestellte Sprache für den lokalen Erkenner, damit das Vorladen bei Schweizerdeutsch und Mundart gleich das Schweizer
    /// Modell lädt (Playbook Abschnitt 26). Jeder Lauf wählt das Modell ohnehin nach seiner eigenen Sprache.
    /// </summary>
    /// <remarks>
    /// Braucht die neue Sprache ein anderes Modell als das schon geladene, lädt es im Hintergrund um, damit das nächste lokale Diktat
    /// nicht darauf wartet. Ist noch keines geladen, übernimmt das Vorladen beim Start (nach der Stilleerkennung) die Wahl.
    /// </remarks>
    public string? LocalLanguage
    {
        get => _local?.PreferredLanguage;
        set
        {
            LocalWhisperTranscriber? local = _local;
            if (local is null) return;

            local.PreferredLanguage = value;
            if (local.NeedsOtherModel(value) && Volatile.Read(ref _disposed) == 0) _ = PreloadLocalAsync();
        }
    }

    /// <summary>True, wenn ein Groq Schlüssel hinterlegt ist. Der Wert selbst wird nicht herausgegeben.</summary>
    public bool HasKey => !string.IsNullOrWhiteSpace(ReadGroqKey());

    /// <summary>True, wenn ein zweiter Erkenner eingesetzt ist.</summary>
    public bool HasSecondary => _secondary is not null;

    /// <summary>
    /// Die eine Stelle, an der die Erkennung der App zusammengesteckt wird, für das Testfenster und für die Befehlszeile.
    /// Hier wird der lokale Erkenner (Whisper.net) als zweiter Erkenner eingesetzt. Dabei wird noch kein Modell geladen;
    /// fehlt die Modelldatei, meldet der lokale Erkenner das erst, wenn er gebraucht wird.
    /// </summary>
    public static SpeechService CreateDefault()
    {
        ITranscriber secondary = new LocalWhisperTranscriber(TalkPaths.WhisperModelPath, swissModelPath: TalkPaths.SwissWhisperModelPath);

        return new SpeechService(new CredentialSecretStore(), TalkHttp.Client, secondary);
    }

    /// <summary>
    /// Erkennt den Text im Clip über die Weiche: zuerst Groq, bei Ausfall der zweite Erkenner; mit <see cref="LocalOnly"/> oder
    /// <paramref name="forceLocal"/> nur der zweite.
    /// Läuft auf dem Thread Pool, damit der Aufrufer (das Fenster) nie blockiert.
    /// </summary>
    /// <param name="clip">Der zugeschnittene Clip. Er bleibt unverändert und wird nie auf die Festplatte geschrieben.</param>
    /// <param name="language">Sprachkürzel ("de", "it", "en", "de-ch", "gsw") oder "auto".</param>
    /// <param name="vocabulary">Begriffe aus dem Wörterbuch in Rangfolge; leer, wenn es keine gibt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <param name="forceLocal">
    /// True erzwingt für diesen einen Lauf "nur lokal", auch wenn <see cref="LocalOnly"/> aus ist: Groq wird nicht gerufen und es geht
    /// nichts ins Netz. Gedacht für Clips ohne Sprachbefund (das VAD Modell fehlt) und für Läufe, deren Hinweis das lokale Wörterbuch enthält. Fehlt der lokale Erkenner, kommt
    /// <see cref="TranscriptionError.LocalUnavailable"/>.
    /// </param>
    /// <returns>Das Ergebnis mit Text, Zeiten und Limit Angaben.</returns>
    /// <exception cref="TranscriptionException">Wenn kein Erkenner ein Ergebnis liefert.</exception>
    /// <exception cref="OperationCanceledException">Bei Abbruch durch den Aufrufer oder durch <see cref="Dispose"/>.</exception>
    public async Task<TranscriptionResult> TranscribeAsync(
        AudioClip clip,
        string language,
        IReadOnlyList<string> vocabulary,
        CancellationToken ct,
        bool forceLocal = false)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        TranscriptionRequest request = new(
            string.IsNullOrWhiteSpace(language) ? TranscriptionRequest.AutoLanguage : language.Trim(),
            vocabulary is { Count: > 0 } ? vocabulary : null);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeToken);
        CancellationToken token = linked.Token;

        // Mit forceLocal läuft die Weiche, die fest auf "nur lokal" steht; sie ruft Groq nie.
        FallbackTranscriber route = forceLocal ? _localRoute : _fallback;

        try
        {
            return await Task.Run(() => route.TranscribeAsync(clip, request, token), token).ConfigureAwait(false);
        }
        finally
        {
            // Hat dieser Lauf das lokale Modell geladen, stehen Ladezeit und Laufzeit jetzt einmal im Protokoll.
            ReportLocalLoaded();
        }
    }

    /// <summary>
    /// Glättet den Text über Groq (Playbook Abschnitt 21). Mit <see cref="LocalOnly"/> wird nichts gesendet. An den Dienst gehen nur
    /// die feste Anweisung und der Text. Wirft nie, außer bei Abbruch: Jeder Fehler ergibt den unveränderten Text mit dem Grund.
    /// </summary>
    public async Task<SmoothingResult> SmoothAsync(string text, SmoothingLevel level, string language, CancellationToken ct)
    {
        if (_localOnly) return new SmoothingResult(text ?? string.Empty, SmoothingOutcome.LocalOnly, 0);
        if (Volatile.Read(ref _disposed) != 0) return new SmoothingResult(text ?? string.Empty, SmoothingOutcome.ServiceError, 0);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeToken);
        return await Task.Run(() => _smoother.SmoothAsync(text ?? string.Empty, level, language, linked.Token), linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Lädt das lokale Modell vorab im Hintergrund, damit das erste lokale Diktat nicht auf das Laden wartet. Es geht nichts ins Netz.
    /// Protokoll: <c>stt | local | geladen ms=N laufzeit=vulkan/cpu/unbekannt</c> nach dem Laden, mit <c>modell=schweiz</c>, wenn das
    /// Schweizer Modell geladen ist, <c>stt | local | fehlt</c> (einmal),
    /// wenn die Modelldatei nicht da ist, <c>stt | local | fehler</c>, wenn sie sich nicht laden lässt. Wirft nie.
    /// </summary>
    /// <returns>Der Zustand des lokalen Modells, für den Hinweis im Fenster.</returns>
    public async Task<LocalModelState> PreloadLocalAsync()
    {
        LocalWhisperTranscriber? local = _local;
        if (local is null || Volatile.Read(ref _disposed) != 0) return LocalModelState.None;

        try
        {
            // Auch die Prüfung der Datei läuft im Hintergrund, der Aufrufer wartet auf nichts.
            bool exists = await Task.Run(() => local.ModelFileExists).ConfigureAwait(false);
            if (!exists) return ReportLocalMissing();

            await local.EnsureLoadedAsync(_lifetimeToken).ConfigureAwait(false);
            ReportLocalLoaded();
            return LocalModelState.Loaded;
        }
        catch (TranscriptionException ex) when (ex.Error == TranscriptionError.ModelMissing)
        {
            return ReportLocalMissing();
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Die App wird gerade beendet.
            return LocalModelState.None;
        }
        catch (Exception)
        {
            // Die Meldung der Ausnahme wird nicht protokolliert, nur das feste Wort.
            SttReport.WriteLocalFailed();
            return LocalModelState.Failed;
        }
    }

    /// <summary>
    /// Baut die Verbindung zu Groq vorab auf, damit die Erkennung nach dem Stopp schneller ist. Passiert nur, wenn nicht
    /// <see cref="LocalOnly"/> gilt und ein Schlüssel hinterlegt ist. Es geht weder Schlüssel noch Audio hinaus.
    /// Protokoll: <c>stt | warmup | ms=N ok=ja/nein</c>. Wirft nie.
    /// </summary>
    public Task WarmUpAsync()
    {
        if (Volatile.Read(ref _disposed) != 0 || _localOnly) return Task.CompletedTask;

        // Auch das Lesen der Schlüsselablage läuft im Hintergrund, der Aufrufer wartet auf nichts.
        return Task.Run(async () =>
        {
            try
            {
                if (_localOnly || !HasKey) return;

                WarmUpResult result = await TalkHttp.WarmUpAsync(_http, _lifetimeToken).ConfigureAwait(false);
                if (Volatile.Read(ref _disposed) != 0) return;

                AppLog.Write("stt", "warmup", $"ms={result.ElapsedMs} ok={(result.Success ? "ja" : "nein")}");
            }
            catch
            {
                // Der vorab aufgebaute Verbindungsaufbau ist nur eine Abkürzung. Scheitert er, baut die Erkennung die Verbindung selbst auf.
            }
        });
    }

    /// <summary>
    /// Prüft einen Groq Schlüssel mit einer Leseanfrage ohne Audio. Protokoll: <c>key | check | ergebnis=gueltig/ungueltig/keine_verbindung/fehlt</c>.
    /// </summary>
    /// <param name="candidate">Ein noch nicht gespeicherter Schlüssel aus dem Dialog; leer oder null prüft den hinterlegten Schlüssel.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>
    /// Das Ergebnis der Prüfung, oder null, wenn <see cref="LocalOnly"/> gilt: Dann geht nichts ins Internet und es wird nichts geprüft.
    /// </returns>
    /// <exception cref="OperationCanceledException">Bei Abbruch durch den Aufrufer oder durch <see cref="Dispose"/>.</exception>
    public async Task<KeyCheckResult?> CheckKeyAsync(string? candidate, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_localOnly) return null;

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeToken);
        CancellationToken token = linked.Token;

        KeyCheckResult result = await Task.Run(
            async () =>
            {
                string? key = string.IsNullOrWhiteSpace(candidate) ? ReadGroqKey() : candidate.Trim();
                try
                {
                    return await GroqKeyChecker.CheckAsync(_http, key, token).ConfigureAwait(false);
                }
                catch (FormatException)
                {
                    // Der Wert enthält Zeichen, die in keiner Kopfzeile stehen dürfen (zum Beispiel einen Zeilenumbruch). So ein Schlüssel kann nicht gelten.
                    return KeyCheckResult.Invalid;
                }
            },
            token).ConfigureAwait(false);

        AppLog.Write("key", "check", $"ergebnis={SttReport.KeyCheckWord(result)}");
        return result;
    }

    /// <summary>
    /// Bricht laufende Anfragen ab und gibt den zweiten Erkenner frei, falls er das braucht. Der lokale Erkenner wartet dabei auf
    /// laufende Arbeit, bevor er sein Modell freigibt. Der gemeinsame HTTP Client bleibt bestehen. Mehrfacher Aufruf ist folgenlos.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        try
        {
            _lifetime.Cancel();
        }
        catch
        {
            // Ein Fehler in einem Abbruch Rückruf darf das Aufräumen nicht stören.
        }

        try
        {
            (_secondary as IDisposable)?.Dispose();
        }
        catch
        {
            // Beim Freigeben nicht mehr wichtig.
        }

        _lifetime.Dispose();
    }

    // Schreibt stt | local | geladen genau einmal, sobald das lokale Modell geladen ist; sonst passiert nichts.
    // Die Laufzeit ist die tatsächlich geladene, nicht die gewünschte. Ließ sich das Schweizer Modell nicht laden, steht davor einmal
    // stt | local | fehler modell=schweiz.
    private void ReportLocalLoaded()
    {
        if (_local is not null && _local.TryTakeSwissFailureReport()) AppLog.Write("stt", "local", "fehler modell=schweiz");

        if (_local is not null && _local.TryTakeLoadReport(out long loadMs))
        {
            SttReport.WriteLocalLoaded(loadMs, WhisperRuntime.LoadedName, _local.LoadedSwiss);
        }
    }

    // Schreibt stt | local | fehlt höchstens einmal je Dienst.
    private LocalModelState ReportLocalMissing()
    {
        if (Interlocked.Exchange(ref _localMissingLogged, 1) == 0) SttReport.WriteLocalMissing();
        return LocalModelState.Missing;
    }

    // Liest den Groq Schlüssel frisch aus der Ablage. Der Wert wird nie gehalten.
    private string? ReadGroqKey() => _secrets.Get(SecretNames.GroqApiKey);
}
