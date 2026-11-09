using System.Net.Http.Headers;

namespace Talk.Core.Transcription;

/// <summary>Ergebnis einer Schlüsselprüfung.</summary>
public enum KeyCheckResult
{
    /// <summary>Der Dienst nimmt den Schlüssel an.</summary>
    Valid,

    /// <summary>Der Dienst lehnt den Schlüssel ab.</summary>
    Invalid,

    /// <summary>Der Dienst ist nicht erreichbar oder antwortet unerwartet.</summary>
    Unreachable,

    /// <summary>Es ist kein Schlüssel vorhanden.</summary>
    Missing,
}

/// <summary>
/// Prüft einen Groq Schlüssel mit einer billigen Leseanfrage (Liste der Modelle). Es wird kein Audio gesendet.
/// Der Schlüssel steht nie in einer Meldung oder Ausnahme.
/// </summary>
public static class GroqKeyChecker
{
    /// <summary>Adresse der Modellliste.</summary>
    public const string DefaultModelsEndpoint = "https://api.groq.com/openai/v1/models";

    /// <summary>
    /// Fragt die Modellliste mit dem Schlüssel ab. Status 200 bis 299 ergibt <see cref="KeyCheckResult.Valid"/>,
    /// 401 und 403 ergeben <see cref="KeyCheckResult.Invalid"/>, alles andere (auch Netzfehler und Zeitüberschreitung)
    /// <see cref="KeyCheckResult.Unreachable"/>. Ohne Schlüssel kommt <see cref="KeyCheckResult.Missing"/> zurück, ohne Anfrage.
    /// Ein Abbruch durch den Aufrufer wird als Abbruch weitergegeben.
    /// </summary>
    public static async Task<KeyCheckResult> CheckAsync(
        HttpClient http,
        string? apiKey,
        CancellationToken ct,
        string endpoint = DefaultModelsEndpoint,
        int timeoutMs = 8000)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Die Wartezeit muss größer als 0 sein.");
        if (string.IsNullOrWhiteSpace(apiKey)) return KeyCheckResult.Missing;

        // Hat der Aufrufer schon abgebrochen, geht keine Anfrage mehr hinaus, auch wenn die Übertragung das Signal selbst nicht beachtet.
        ct.ThrowIfCancellationRequested();

        using HttpRequestMessage message = new(HttpMethod.Get, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(timeoutMs);

        try
        {
            using HttpResponseMessage response = await http
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            int code = (int)response.StatusCode;
            if (code is >= 200 and <= 299) return KeyCheckResult.Valid;
            if (code is 401 or 403) return KeyCheckResult.Invalid;
            return KeyCheckResult.Unreachable;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return KeyCheckResult.Unreachable;
        }
        catch (HttpRequestException)
        {
            return KeyCheckResult.Unreachable;
        }
    }
}
