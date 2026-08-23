using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Der gemeinsame Weg nach draussen fuer die drei Auskunftsleser: Zeitlimit,
/// Abbruch und Aufrufe der Reihe nach.
///
/// Die Abfragen gehen an einen oeffentlichen Dienst des Kantons. Sie laufen
/// deshalb bewusst NACHEINANDER — ein Schwall gleichzeitiger Anfragen waere
/// unhoeflich und kann gesperrt werden.
///
/// Ein Fehler ergibt null. Der Aufrufer meldet das als Warnung; nichts wird
/// geraten.
/// </summary>
public sealed class GeoUrHttpGateway : IDisposable
{
    private static readonly TimeSpan Standardzeit = TimeSpan.FromSeconds(45);

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _einerNachDemAnderen = new(1, 1);
    private readonly bool _eigenerClient;

    public GeoUrHttpGateway(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = timeout ?? Standardzeit;
        _eigenerClient = true;

        // Ein sprechender Absender ist bei einem fremden Dienst Anstand.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SewerStudio/1.0");
    }

    public async Task<string?> GetStringAsync(Uri uri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return await SendeAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), ct)
            .ConfigureAwait(false);
    }

    public async Task<string?> PostFormAsync(
        Uri uri, IReadOnlyDictionary<string, string> form, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(form);

        return await SendeAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new FormUrlEncodedContent(form)
            },
            ct).ConfigureAwait(false);
    }

    private async Task<string?> SendeAsync(
        Func<HttpRequestMessage> baueAnfrage, CancellationToken ct)
    {
        await _einerNachDemAnderen.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var anfrage = baueAnfrage();
            using var antwort = await _http.SendAsync(anfrage, ct).ConfigureAwait(false);

            if (!antwort.IsSuccessStatusCode)
                return null;

            var bytes = await antwort.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return LiesText(bytes, antwort.Content.Headers.ContentType?.CharSet);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Dossier-Auskunft] Abfrage fehlgeschlagen: {ex.Message}");
            return null;
        }
        finally
        {
            _einerNachDemAnderen.Release();
        }
    }

    /// <summary>
    /// Die Grundbuchauskunft ist ISO-8859-1. Wird sie als UTF-8 gelesen, wird
    /// aus einem Umlaut ein Fragezeichen — und damit steht ein verstuemmelter
    /// Name im Brief an den Eigentuemer.
    ///
    /// "ISO-8859-1" braucht unter .NET normalerweise die Zusatzkodierungen aus
    /// System.Text.Encoding.CodePages. Dieses Projekt bindet dafuer kein neues
    /// Paket ein; deshalb faellt der Aufruf bei einer ArgumentException auf das
    /// seit .NET 5 fest eingebaute <see cref="Encoding.Latin1"/> zurueck, das
    /// mit ISO-8859-1 identisch ist.
    /// </summary>
    private static string LiesText(byte[] bytes, string? charSet)
    {
        var kodierung = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                kodierung = Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                kodierung = Encoding.Latin1;
            }
        }

        return kodierung.GetString(bytes);
    }

    public void Dispose()
    {
        if (_eigenerClient)
            _http.Dispose();

        _einerNachDemAnderen.Dispose();
    }
}
