// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Net;
// Die Windows Desktop SDK nimmt System.Net.Http aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.Net.Http;
using System.Net.Http.Headers;

using Talk.Core;

namespace Talk.App.Net;

/// <summary>Ergebnis des vorab aufgebauten Verbindungsaufbaus.</summary>
/// <param name="ElapsedMs">Dauer in Millisekunden.</param>
/// <param name="Success">True, wenn der Dienst geantwortet hat (der Status ist dabei egal).</param>
internal readonly record struct WarmUpResult(long ElapsedMs, bool Success);

/// <summary>
/// Der eine gemeinsame HTTP Client der App. Verbindungen werden wiederverwendet, HTTP/2 wird benutzt, wenn der Dienst es anbietet.
/// </summary>
/// <remarks>
/// <para>
/// Talk spricht im Netz nur mit <c>api.groq.com</c> über HTTPS. Jede andere Adresse wird abgewiesen, bevor eine Verbindung entsteht.
/// Ob überhaupt gesendet werden darf (Schalter "nur lokal"), entscheidet <see cref="Transcription.SpeechService"/>.
/// </para>
/// <para>
/// Der Client lebt bis zum Ende des Programms und wird nie freigegeben. Er trägt keinen Schlüssel; den setzt der Erkenner je Anfrage.
/// </para>
/// </remarks>
internal static class TalkHttp
{
    /// <summary>Der einzige Rechner, mit dem Talk im Netz spricht.</summary>
    public const string AllowedHost = "api.groq.com";

    private const string WarmUpAddress = "https://api.groq.com/";

    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    // So lange bleibt eine unbenutzte Verbindung offen. Die Vorgabe von .NET ist 1 Minute; damit wäre die vorab aufgebaute
    // Verbindung bei einem längeren Diktat schon wieder zu, bevor das Audio geht.
    private static readonly TimeSpan ConnectionIdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromSeconds(3);

    private static readonly Lazy<HttpClient> Shared = new(CreateClient, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Der gemeinsame Client. Entsteht beim ersten Zugriff.</summary>
    public static HttpClient Client => Shared.Value;

    /// <summary>
    /// Baut die Verbindung zu Groq vorab auf: eine HEAD Anfrage ohne Schlüssel und ohne Audio, höchstens 3 Sekunden.
    /// Fehler werden geschluckt; das Ergebnis nennt nur Dauer und Erfolg.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    public static Task<WarmUpResult> WarmUpAsync(CancellationToken ct) => WarmUpAsync(Client, ct);

    /// <summary>
    /// Wie <see cref="WarmUpAsync(CancellationToken)"/>, aber über den angegebenen Client. So wärmt die Verdrahtung genau die
    /// Verbindung vor, über die danach das Audio geht.
    /// </summary>
    /// <param name="http">Der Client, dessen Verbindung aufgebaut werden soll.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public static async Task<WarmUpResult> WarmUpAsync(HttpClient http, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);

        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(WarmUpTimeout);

            using HttpRequestMessage request = new(HttpMethod.Head, WarmUpAddress);
            using HttpResponseMessage response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            // Jede Antwort zeigt, dass die Verbindung steht. Der Status spielt keine Rolle.
            return new WarmUpResult(watch.ElapsedMilliseconds, true);
        }
        catch
        {
            // Kein Netz, Zeitüberschreitung oder Abbruch: Die eigentliche Anfrage baut die Verbindung dann selbst auf.
            return new WarmUpResult(watch.ElapsedMilliseconds, false);
        }
    }

    private static HttpClient CreateClient()
    {
        SocketsHttpHandler sockets = new()
        {
            PooledConnectionLifetime = ConnectionLifetime,
            PooledConnectionIdleTimeout = ConnectionIdleTimeout,
            ConnectTimeout = ConnectTimeout,
            AutomaticDecompression = DecompressionMethods.All,

            // Keine Weiterleitung folgen: Sie könnte zu einem anderen Rechner führen, vorbei an der Prüfung in GroqOnlyHandler.
            AllowAutoRedirect = false,
        };

        HttpClient client = new(new GroqOnlyHandler(sockets), disposeHandler: true)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,

            // Rückhalt. Die eigentlichen Grenzen setzen die Erkenner je Anfrage.
            Timeout = OverallTimeout,
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(TalkInfo.Name, TalkInfo.Version));
        return client;
    }

    /// <summary>
    /// Lässt nur Anfragen an <see cref="AllowedHost"/> über HTTPS durch und hebt jede Anfrage auf HTTP/2 mit Rückfall auf HTTP/1.1.
    /// </summary>
    /// <remarks>
    /// Die Vorgabe <see cref="HttpClient.DefaultRequestVersion"/> gilt nur für Anfragen, die der Client selbst baut. Die Erkenner bauen
    /// ihre Anfrage selbst, sie trüge sonst HTTP/1.1 und liefe an der vorab aufgebauten HTTP/2 Verbindung vorbei.
    /// </remarks>
    private sealed class GroqOnlyHandler : DelegatingHandler
    {
        public GroqOnlyHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Prepare(request);
            return base.SendAsync(request, cancellationToken);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Prepare(request);
            return base.Send(request, cancellationToken);
        }

        private static void Prepare(HttpRequestMessage request)
        {
            Uri? address = request.RequestUri;
            if (address is null
                || !address.IsAbsoluteUri
                || address.Scheme != Uri.UriSchemeHttps
                || !string.Equals(address.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
            {
                // Die Adresse steht bewusst nicht in der Meldung.
                throw new HttpRequestException("Talk sendet nur an api.groq.com über HTTPS.");
            }

            if (request.Version < HttpVersion.Version20)
            {
                request.Version = HttpVersion.Version20;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            }
        }
    }
}
