using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Schlaegt Schachtfelder im lokalen Abwasserkataster nach. Rein lesend, ohne
/// Netzzugriff. Liefert ausserdem die Lage eines Schachts — sie ist die
/// Grundlage fuer den Grundbuchweg, weil Projektdatensaetze keine Koordinaten
/// fuehren.
/// </summary>
public sealed class KatasterFeldNachschlag : IFeldWertNachschlag
{
    private readonly ISchachtCadastreTableStore _store;
    private readonly string _tabellenPfad;
    private readonly string _xtfPfad;
    private readonly Func<string, bool> _xtfVorhanden;

    public KatasterFeldNachschlag(
        ISchachtCadastreTableStore store,
        string tabellenPfad,
        string xtfPfad,
        Func<string, bool>? xtfVorhanden = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tabellenPfad = tabellenPfad ?? throw new ArgumentNullException(nameof(tabellenPfad));
        _xtfPfad = xtfPfad ?? throw new ArgumentNullException(nameof(xtfPfad));
        _xtfVorhanden = xtfVorhanden ?? File.Exists;
    }

    public async Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_xtfPfad) || !_xtfVorhanden(_xtfPfad))
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                "Der Abwasserkataster ist nicht eingerichtet. "
                + "Die XTF-Datei laesst sich in den Einstellungen hinterlegen.");
        }

        // Beim ersten Aufruf entsteht die Tabelle aus einer mehrere hundert
        // Megabyte grossen Datei. Das darf die Oberflaeche nicht einfrieren.
        return await Task.Run(() => Suche(anfrage), ct).ConfigureAwait(false);
    }

    private FeldNachschlagErgebnis Suche(FeldNachschlagAnfrage anfrage)
    {
        try
        {
            var treffer = SucheSchaechte(anfrage.Schachtnummer);

            if (treffer.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Schacht {anfrage.Schachtnummer} steht nicht im Abwasserkataster.");
            }

            var vorschlaege = treffer
                .Select(s => LiesFeld(s, anfrage.Feldname))
                .Where(wert => !KatasterPlatzhalter.IstPlatzhalter(wert))
                .Select(wert => new FeldVorschlag(wert!.Trim(), "Abwasserkataster", HerkunftKataster))
                .ToList();

            if (vorschlaege.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Der Abwasserkataster fuehrt fuer {anfrage.Feldname} keinen Wert.");
            }

            // Zwei Schaechte mit derselben Nummer: nicht raten, sondern fragen.
            return vorschlaege.Count == 1
                ? new FeldNachschlagErgebnis.Gefunden(vorschlaege[0])
                : new FeldNachschlagErgebnis.Mehrdeutig(vorschlaege);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FeldNachschlagErgebnis.Fehler(ex.Message);
        }
    }

    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftKataster = "Kataster";

    /// <summary>
    /// Die Lage eines eindeutig bestimmten Schachts. Bei mehrdeutiger oder
    /// unbekannter Nummer bewusst null — eine geratene Lage waere schlimmer
    /// als keine.
    /// </summary>
    public (double Ost, double Nord)? LiesLage(string schachtnummer)
    {
        if (string.IsNullOrWhiteSpace(_xtfPfad) || !_xtfVorhanden(_xtfPfad))
            return null;

        try
        {
            var treffer = SucheSchaechte(schachtnummer);
            if (treffer.Count != 1)
                return null;

            var schacht = treffer[0];
            return schacht.Ost.HasValue && schacht.Nord.HasValue
                ? (schacht.Ost.Value, schacht.Nord.Value)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private List<CadastreSchacht> SucheSchaechte(string? schachtnummer)
    {
        if (!_store.IsTableFresh(_tabellenPfad, _xtfPfad))
            _store.BuildTable(_xtfPfad, _tabellenPfad);

        var gesucht = (schachtnummer ?? string.Empty).Trim();
        if (gesucht.Length == 0)
            return [];

        return _store.ReadTable(_tabellenPfad)
            .Where(s => string.Equals(
                s.Bezeichnung?.Trim(), gesucht, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? LiesFeld(CadastreSchacht schacht, string feldname) => feldname switch
    {
        "Funktion" => schacht.Funktion,
        "Material" => schacht.Material,
        _ => null
    };
}
