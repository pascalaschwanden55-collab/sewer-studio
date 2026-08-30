using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Schlaegt Schachtfelder im Abwassernetz des Kantons nach.
///
/// Wichtig beim Feld "Eigentuemer": Gemeint ist der Eigentuemer des BAUWERKS
/// (Privat, Abwasser Uri, Kanton Uri, eine Gemeinde). Das ist etwas anderes
/// als der Grundstuecksbesitzer im Eigentuemerdossier — bei manchen Anlagen
/// gehoert das Bauwerk nicht dem, dem das Land gehoert.
///
/// Die XTF taugt dafuer nicht: Dort tragen alle Bauwerke denselben Verweis.
/// </summary>
public sealed class SchachtNetzFeldNachschlag : IFeldWertNachschlag
{
    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftNetz = "Abwassernetz";

    private readonly ISchachtNetzLookup _netz;
    private readonly Action<string>? _log;

    public SchachtNetzFeldNachschlag(ISchachtNetzLookup netz, Action<string>? log = null)
    {
        _netz = netz ?? throw new ArgumentNullException(nameof(netz));
        _log = log;
    }

    public async Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        var nummer = (anfrage.Bauteilnummer ?? string.Empty).Trim();
        if (nummer.Length == 0)
            return new FeldNachschlagErgebnis.NichtGefunden("Ohne Schachtnummer keine Abfrage.");

        try
        {
            // Genau ein Name je Anfrage — kein Sammellauf.
            var treffer = await _netz.FindByNamesAsync([nummer], ct).ConfigureAwait(false);

            if (treffer.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Schacht {nummer} steht nicht im Abwassernetz des Kantons.");
            }

            var werte = treffer
                .Select(s => LiesFeld(s, anfrage.Feldname))
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => new FeldVorschlag(w!.Trim(), Quelle(nummer), HerkunftNetz))
                .ToList();

            if (werte.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Das Abwassernetz fuehrt fuer {anfrage.Feldname} keinen Wert.");
            }

            _log?.Invoke("Abwassernetz-Abfrage erfolgreich.");

            // Zwei Bauwerke gleicher Nummer: nicht raten, sondern fragen.
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

    private static string? LiesFeld(NetworkSchacht schacht, string feldname) => feldname switch
    {
        var f when f.StartsWith("Eigent", StringComparison.OrdinalIgnoreCase) => schacht.Eigentuemer,
        "Funktion" => schacht.Funktion,
        "Material" => schacht.Material,
        "Nutzungsart" or "Nutzungsart_Ist" => schacht.Nutzungsart,
        _ => null
    };

    private static string Quelle(string nummer)
        => $"Abwassernetz Kanton Uri, Schacht {nummer}";

    private static bool IstDrosselung(Exception ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
           || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
}
