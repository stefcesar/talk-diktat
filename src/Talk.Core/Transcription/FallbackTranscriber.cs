// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Audio;

namespace Talk.Core.Transcription;

/// <summary>
/// Einstellungen der Weiche zwischen erstem und zweitem Erkenner.
/// </summary>
/// <param name="RetryDelayMs">Wartezeit vor dem einen Wiederholungsversuch bei Netzfehler oder Fehler des Dienstes. Standard 300 ms.</param>
/// <param name="MaxRetryAfterMs">Höchste vom Dienst genannte Wartezeit, die bei erreichtem Limit noch abgewartet wird. Standard 2.000 ms.</param>
public sealed record FallbackOptions(int RetryDelayMs = 300, int MaxRetryAfterMs = 2000)
{
    /// <summary>Wirft bei unbrauchbaren Werten.</summary>
    public void Validate()
    {
        if (RetryDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(RetryDelayMs), "RetryDelayMs darf nicht negativ sein.");
        if (MaxRetryAfterMs < 0) throw new ArgumentOutOfRangeException(nameof(MaxRetryAfterMs), "MaxRetryAfterMs darf nicht negativ sein.");
    }
}

/// <summary>
/// Weiche: nimmt den ersten Erkenner (Groq), wiederholt bei kurzen Störungen einmal und springt sonst auf den zweiten
/// (lokal). Im Modus "nur lokal" wird der erste nie angesprochen. So geht kein Diktat verloren, solange einer der beiden läuft.
/// </summary>
/// <remarks>
/// Regeln:
/// 1. Nur lokal: Liefert die Funktion true, läuft nur der zweite Erkenner; seine Fehler gehen unverändert weiter.
///    Fehlt er, kommt <see cref="TranscriptionError.LocalUnavailable"/>. Der erste wird nicht gerufen.
/// 2. Erfolg des ersten Erkenners geht unverändert zurück (ohne Grund für einen Wechsel).
/// 3. Wiederholung, genau einmal: bei <see cref="TranscriptionError.Network"/> und <see cref="TranscriptionError.ServerError"/>
///    nach <see cref="FallbackOptions.RetryDelayMs"/>; bei <see cref="TranscriptionError.RateLimited"/> nur, wenn der Dienst eine
///    Wartezeit nennt, die höchstens <see cref="FallbackOptions.MaxRetryAfterMs"/> beträgt, dann nach genau dieser Wartezeit.
///    Alle anderen Fehler werden nicht wiederholt.
/// 4. Wechsel: Scheitert der erste Erkenner endgültig, läuft der zweite; sein Ergebnis trägt als Grund den letzten Fehler des ersten.
/// 5. Sperre: Endet der erste Erkenner mit erreichtem Limit und genannter Wartezeit, wird er bis zum Ablauf dieser Wartezeit
///    nicht mehr angesprochen; in dieser Zeit läuft sofort der zweite, Grund <see cref="TranscriptionError.RateLimited"/>.
/// 6. Ohne zweiten Erkenner geht der letzte Fehler des ersten weiter (während der Sperre das erreichte Limit mit Restwartezeit).
/// 7. Scheitern beide, geht der Fehler des ersten weiter; der Fehler des zweiten hängt als innere Ausnahme an.
/// 8. Jede andere Ausnahme eines Erkenners außer einem Abbruch durch den Aufrufer zählt als
///    <see cref="TranscriptionError.EngineFailed"/>; ein interner Abbruch ohne Abbruch des Aufrufers zählt als
///    <see cref="TranscriptionError.Timeout"/>.
/// 9. Ein Abbruch durch den Aufrufer geht sofort als Abbruch weiter, ohne Wiederholung und ohne Wechsel.
/// </remarks>
public sealed class FallbackTranscriber : ITranscriber
{
    /// <summary>Name der Weiche. Ergebnisse tragen den Namen des Erkenners, der tatsächlich geliefert hat.</summary>
    public const string EngineName = "auto";

    private readonly ITranscriber _primary;
    private readonly ITranscriber? _secondary;
    private readonly Func<bool> _localOnly;
    private readonly FallbackOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();

    private DateTimeOffset? _primaryBlockedUntil;

    /// <summary>Erzeugt die Weiche.</summary>
    /// <param name="primary">Erster Erkenner (Groq).</param>
    /// <param name="secondary">Zweiter Erkenner (lokal) oder null, wenn keiner vorhanden ist.</param>
    /// <param name="localOnly">Liefert true, wenn nur lokal erkannt werden darf. Wird je Anfrage gerufen.</param>
    /// <param name="options">Einstellungen oder null für die Standardwerte.</param>
    /// <param name="delay">Wartefunktion; null benutzt <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Für Tests austauschbar.</param>
    /// <param name="now">Uhr; null benutzt die Systemzeit. Für Tests austauschbar.</param>
    public FallbackTranscriber(
        ITranscriber primary,
        ITranscriber? secondary,
        Func<bool> localOnly,
        FallbackOptions? options = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<DateTimeOffset>? now = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _secondary = secondary;
        _localOnly = localOnly ?? throw new ArgumentNullException(nameof(localOnly));
        _options = options ?? new FallbackOptions();
        _options.Validate();
        _delay = delay ?? Task.Delay;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public string Name => EngineName;

    /// <summary>Zeitpunkt, bis zu dem der erste Erkenner wegen erreichtem Limit nicht angesprochen wird, oder null.</summary>
    public DateTimeOffset? PrimaryBlockedUntil
    {
        get { lock (_gate) return _primaryBlockedUntil; }
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);

        if (_localOnly())
        {
            if (_secondary is null)
            {
                throw new TranscriptionException(TranscriptionError.LocalUnavailable, "Es ist kein lokaler Erkenner vorhanden.");
            }

            return await RunAsync(_secondary, clip, request, ct).ConfigureAwait(false);
        }

        TranscriptionException failure;
        TimeSpan? blockedFor = RemainingBlock();

        if (blockedFor is TimeSpan remaining)
        {
            failure = new TranscriptionException(
                TranscriptionError.RateLimited,
                "Das Limit des ersten Erkenners ist erreicht, er bleibt vorerst gesperrt.",
                null,
                remaining);
        }
        else
        {
            try
            {
                return await RunAsync(_primary, clip, request, ct).ConfigureAwait(false);
            }
            catch (TranscriptionException ex)
            {
                failure = ex;
            }

            if (TryGetRetryWait(failure, out TimeSpan wait))
            {
                await _delay(wait, ct).ConfigureAwait(false);

                try
                {
                    return await RunAsync(_primary, clip, request, ct).ConfigureAwait(false);
                }
                catch (TranscriptionException ex)
                {
                    failure = ex;
                }
            }

            if (failure.Error == TranscriptionError.RateLimited && failure.RetryAfter is TimeSpan retryAfter && retryAfter > TimeSpan.Zero)
            {
                lock (_gate) _primaryBlockedUntil = _now() + retryAfter;
            }
        }

        if (_secondary is null) throw failure;

        try
        {
            TranscriptionResult result = await RunAsync(_secondary, clip, request, ct).ConfigureAwait(false);
            return result with { FallbackReason = failure.Error };
        }
        catch (TranscriptionException secondaryFailure)
        {
            throw new TranscriptionException(failure.Error, failure.Message, secondaryFailure, failure.RetryAfter, failure.ServerCode);
        }
    }

    // Ruft einen Erkenner und übersetzt fremde Ausnahmen nach Regel 8. Ein Abbruch durch den Aufrufer geht unverändert weiter.
    private static async Task<TranscriptionResult> RunAsync(ITranscriber engine, AudioClip clip, TranscriptionRequest request, CancellationToken ct)
    {
        try
        {
            return await engine.TranscribeAsync(clip, request, ct).ConfigureAwait(false);
        }
        catch (TranscriptionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new TranscriptionException(TranscriptionError.Timeout, "Der Erkenner hat nicht rechtzeitig geantwortet.", ex);
        }
        catch (Exception ex)
        {
            throw new TranscriptionException(TranscriptionError.EngineFailed, "Der Erkenner ist gescheitert.", ex);
        }
    }

    private bool TryGetRetryWait(TranscriptionException failure, out TimeSpan wait)
    {
        switch (failure.Error)
        {
            case TranscriptionError.Network:
            case TranscriptionError.ServerError:
                wait = TimeSpan.FromMilliseconds(_options.RetryDelayMs);
                return true;

            case TranscriptionError.RateLimited
                when failure.RetryAfter is TimeSpan retryAfter
                    && retryAfter <= TimeSpan.FromMilliseconds(_options.MaxRetryAfterMs):
                wait = retryAfter;
                return true;

            default:
                wait = TimeSpan.Zero;
                return false;
        }
    }

    private TimeSpan? RemainingBlock()
    {
        lock (_gate)
        {
            if (_primaryBlockedUntil is not DateTimeOffset until) return null;

            TimeSpan remaining = until - _now();
            if (remaining > TimeSpan.Zero) return remaining;

            _primaryBlockedUntil = null;
            return null;
        }
    }
}
