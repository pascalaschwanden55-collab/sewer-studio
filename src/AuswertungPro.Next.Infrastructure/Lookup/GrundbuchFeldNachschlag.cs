using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Schlaegt Eigentuemer und Adresse in der Grundbuchauskunft des Kantons Uri
/// nach. Der Weg fuehrt ueber die Lage des Schachts: Sie kommt aus dem
/// Kataster, weil Projektdatensaetze keine Koordinaten fuehren.
///
/// Gesucht wird raeumlich und nicht ueber eine Parzellennummer. Damit
/// entfaellt die Gemeinde-Falle — Parzellennummern sind je Gemeinde vergeben,
/// und genau daran ist beim Dossier-Bau schon einmal ein Brief an einen
/// Unbeteiligten entstanden.
/// </summary>
public sealed class GrundbuchFeldNachschlag : IFeldWertNachschlag
{
    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftGrundbuch = "Grundbuch";

    private readonly Func<string, (double Ost, double Nord)?> _lageQuelle;
    private readonly IParcelLookup _parzellen;
    private readonly ILandRegistryLookup _grundbuch;
    private readonly Action<string>? _log;

    public GrundbuchFeldNachschlag(
        Func<string, (double Ost, double Nord)?> lageQuelle,
        IParcelLookup parzellen,
        ILandRegistryLookup grundbuch,
        Action<string>? log = null)
    {
        _lageQuelle = lageQuelle ?? throw new ArgumentNullException(nameof(lageQuelle));
        _parzellen = parzellen ?? throw new ArgumentNullException(nameof(parzellen));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
        _log = log;
    }

    public async Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        try
        {
            // Die Lage kommt aus der Kataster-Tabelle, die beim ersten Aufruf
            // aus einer mehrere hundert Megabyte grossen Datei entsteht. Das
            // darf die Oberflaeche nicht einfrieren.
            var lage = await Task.Run(() => _lageQuelle(anfrage.Schachtnummer), ct)
                .ConfigureAwait(false);
            if (lage is null)
            {
                // Ohne Lage keine Abfrage. Jeder unnoetige Aufruf zaehlt gegen
                // die Drosselung des Kantons.
                return new FeldNachschlagErgebnis.NichtGefunden(
                    "Der Schacht steht nicht mit einer Lage im Abwasserkataster. "
                    + "Ohne Lage laesst sich die Parzelle nicht bestimmen.");
            }

            var linie = PunktAlsKurzeLinie.Baue(lage.Value.Ost, lage.Value.Nord);
            var parzellen = await _parzellen
                .FindTouchedAsync([linie], ct)
                .ConfigureAwait(false);

            if (parzellen.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    "An dieser Lage liegt keine Parzelle des Kantons Uri.");
            }

            if (parzellen.Count > 1)
            {
                // Der Schacht liegt auf einer Parzellengrenze. Frueher wurden
                // hier die Parzellennummern als Vorschlaege geliefert - eine
                // gewaehlte Nummer waere dann als "Eigentuemer" im Protokoll
                // gelandet. Stattdessen ehrlich abbrechen und sagen, wo der
                // Bearbeiter selbst nachsehen kann.
                var nummern = string.Join(", ", parzellen.Select(p => p.Number));
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Der Schacht liegt auf einer Parzellengrenze ({nummern}). "
                    + "Welche Parzelle gemeint ist, laesst sich von hier aus nicht "
                    + "entscheiden.");
            }

            var parzelle = parzellen[0];
            var eintrag = await _grundbuch.ReadAsync(parzelle, ct).ConfigureAwait(false);

            if (eintrag is null || eintrag.NoOwnerRegistered)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Fuer Parzelle {parzelle.Number} ist kein Eigentuemer eingetragen.");
            }

            _log?.Invoke("Grundbuchabfrage erfolgreich.");

            return IstEigentuemerfeld(anfrage.Feldname)
                ? Eigentuemer(eintrag, parzelle)
                : Adresse(eintrag, parzelle);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IstDrosselung(ex))
        {
            // Nur die Fehlerklasse ins Protokoll — nie ein Name, nie eine Adresse.
            _log?.Invoke("Grundbuchabfrage gedrosselt.");
            return new FeldNachschlagErgebnis.Gedrosselt();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Grundbuchabfrage fehlgeschlagen: {ex.GetType().Name}");
            return new FeldNachschlagErgebnis.Fehler(ex.Message);
        }
    }

    private static FeldNachschlagErgebnis Eigentuemer(LandRegistryEntry eintrag, ParcelInfo parzelle)
    {
        var namen = eintrag.Owners
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => new FeldVorschlag(o.Name.Trim(), Quelle(parzelle), HerkunftGrundbuch))
            .ToList();

        if (namen.Count == 0)
            return new FeldNachschlagErgebnis.NichtGefunden("Kein Eigentuemername vorhanden.");

        // Miteigentum und Stockwerkeigentum: alle zur Auswahl stellen.
        return namen.Count == 1
            ? new FeldNachschlagErgebnis.Gefunden(namen[0])
            : new FeldNachschlagErgebnis.Mehrdeutig(namen);
    }

    private static FeldNachschlagErgebnis Adresse(LandRegistryEntry eintrag, ParcelInfo parzelle)
    {
        var teile = new[] { eintrag.BuildingStreet?.Trim(), eintrag.BuildingHouseNumber?.Trim() }
            .Where(t => !string.IsNullOrWhiteSpace(t));
        var strasse = string.Join(' ', teile);

        return string.IsNullOrWhiteSpace(strasse)
            ? new FeldNachschlagErgebnis.NichtGefunden("Keine Gebaeudeadresse eingetragen.")
            : new FeldNachschlagErgebnis.Gefunden(
                new FeldVorschlag(strasse, Quelle(parzelle), HerkunftGrundbuch));
    }

    private static string Quelle(ParcelInfo parzelle)
        => $"Grundbuch Uri, Parzelle {parzelle.Number} ({parzelle.Municipality})";

    private static bool IstEigentuemerfeld(string feldname)
        => feldname.StartsWith("Eigent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Der Kartendienst meldet eine Drosselung als Fehlschlag mit dem
    /// Statuscode im Text ("Der Kartendienst antwortete mit 429.").
    /// </summary>
    private static bool IstDrosselung(Exception ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
           || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
}
