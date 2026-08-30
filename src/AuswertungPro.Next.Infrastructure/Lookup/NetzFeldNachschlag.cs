using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Schlaegt Haltungsfelder im Abwassernetz des Kantons nach — und zwar dort,
/// wo die Angaben noch vollstaendig sind.
///
/// Der Umweg ueber diesen Dienst ist noetig, weil der QGIS-Export nach XTF die
/// Eigentuemer-Zuordnung einplattet: Dort tragen alle Leitungen denselben
/// Verweis, obwohl der Kopf der Datei 27 verschiedene Eigentuemer nennt. Am
/// 2026-08-30 gegen den echten Dienst geprueft: Die Haltungen 36262-36275,
/// 33458-36051 und 36275-35558 liefern "Privat", waehrend die XTF fuer
/// dieselben Leitungen "Abwasser Uri" behauptet haette.
/// </summary>
public sealed class NetzFeldNachschlag : IFeldWertNachschlag
{
    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftNetz = "Abwassernetz";

    private readonly ISewerNetworkLookup _netz;
    private readonly Action<string>? _log;

    public NetzFeldNachschlag(ISewerNetworkLookup netz, Action<string>? log = null)
    {
        _netz = netz ?? throw new ArgumentNullException(nameof(netz));
        _log = log;
    }

    public async Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        var name = (anfrage.Bauteilnummer ?? string.Empty).Trim();
        if (name.Length == 0)
            return new FeldNachschlagErgebnis.NichtGefunden("Ohne Haltungsnamen keine Abfrage.");

        try
        {
            // Genau ein Name je Anfrage — kein Sammellauf.
            var treffer = await _netz.FindByNamesAsync([name], ct).ConfigureAwait(false);

            if (treffer.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Haltung {name} steht nicht im Abwassernetz des Kantons. "
                    + "Private Hausanschluesse fuehrt er nicht.");
            }

            var werte = treffer
                .Select(h => LiesFeld(h, anfrage.Feldname))
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => new FeldVorschlag(w!.Trim(), Quelle(name), HerkunftNetz))
                .ToList();

            if (werte.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Das Abwassernetz fuehrt fuer {anfrage.Feldname} keinen Wert.");
            }

            _log?.Invoke("Abwassernetz-Abfrage erfolgreich.");

            // Zwei Leitungen gleichen Namens: nicht raten, sondern fragen.
            return werte.Count == 1
                ? new FeldNachschlagErgebnis.Gefunden(werte[0])
                : new FeldNachschlagErgebnis.Mehrdeutig(werte);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IstDrosselung(ex))
        {
            _log?.Invoke("Abwassernetz-Abfrage gedrosselt.");
            return new FeldNachschlagErgebnis.Gedrosselt();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Abwassernetz-Abfrage fehlgeschlagen: {ex.GetType().Name}");
            return new FeldNachschlagErgebnis.Fehler(ex.Message);
        }
    }

    private static string? LiesFeld(NetworkHolding haltung, string feldname) => feldname switch
    {
        var f when f.StartsWith("Eigent", StringComparison.OrdinalIgnoreCase) => haltung.Owner,
        "Haltungslaenge_m" => haltung.LengthMeters?.ToString(
            "0.##", System.Globalization.CultureInfo.InvariantCulture),
        "FunktionHierarchisch" => haltung.FunktionHierarchisch,
        "Nutzungsart" or "Nutzungsart_Ist" => haltung.NutzungsartIst,
        "Rohrmaterial" => haltung.Material,
        _ => null
    };

    private static string Quelle(string name)
        => $"Abwassernetz Kanton Uri, Haltung {name}";

    /// <summary>
    /// Der Kartendienst meldet eine Drosselung als Fehlschlag mit dem
    /// Statuscode im Text ("Der Kartendienst antwortete mit 429.").
    /// </summary>
    private static bool IstDrosselung(Exception ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
           || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
}
