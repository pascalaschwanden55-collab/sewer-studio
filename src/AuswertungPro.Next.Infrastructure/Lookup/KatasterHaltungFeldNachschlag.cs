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
/// Schlaegt Haltungsfelder im lokalen Abwasserkataster nach. Material und
/// Laenge stehen bereits in der Tabelle, die der Verteil-Abgleich nutzt —
/// sie wird dafuer nicht veraendert.
///
/// Der Eigentuemer ist ein Sonderfall: Im ganzen Kataster gibt es genau eine
/// Organisation. Der Wert sagt deshalb nicht, WEM die Leitung gehoert,
/// sondern DASS sie dem Kanton gehoert — und unterscheidet damit oeffentliche
/// von privaten Anschluessen. Fuehrt der Kataster spaeter mehrere Betreiber,
/// braucht es die Zuordnung ueber EigentuemerRef; bis dahin wird lieber
/// nichts geliefert als ein falscher Name.
/// </summary>
public sealed class KatasterHaltungFeldNachschlag : IFeldWertNachschlag
{
    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftKataster = "Kataster";

    private readonly IHaltungCadastreTableStore _store;
    private readonly string _tabellenPfad;
    private readonly string _xtfPfad;
    private readonly Func<string, string?> _leseOrganisation;
    private readonly Func<string, bool> _xtfVorhanden;

    public KatasterHaltungFeldNachschlag(
        IHaltungCadastreTableStore store,
        string tabellenPfad,
        string xtfPfad,
        Func<string, string?>? leseOrganisation = null,
        Func<string, bool>? xtfVorhanden = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tabellenPfad = tabellenPfad ?? throw new ArgumentNullException(nameof(tabellenPfad));
        _xtfPfad = xtfPfad ?? throw new ArgumentNullException(nameof(xtfPfad));
        _leseOrganisation = leseOrganisation ?? KatasterOrganisationLeser.LiesEinzigeOrganisation;
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

        // Der erste Aufruf baut die Tabelle aus einer sehr grossen Datei.
        return await Task.Run(() => Suche(anfrage), ct).ConfigureAwait(false);
    }

    private FeldNachschlagErgebnis Suche(FeldNachschlagAnfrage anfrage)
    {
        try
        {
            var treffer = SucheHaltungen(anfrage.Bauteilnummer);

            if (treffer.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Haltung {anfrage.Bauteilnummer} steht nicht im Abwasserkataster. "
                    + "Private Hausanschluesse fuehrt der Kanton nicht.");
            }

            var werte = treffer
                .Select(h => LiesFeld(h, anfrage.Feldname))
                .Where(wert => !KatasterPlatzhalter.IstPlatzhalter(wert))
                .Select(wert => new FeldVorschlag(wert!.Trim(), "Abwasserkataster", HerkunftKataster))
                .ToList();

            if (werte.Count == 0)
            {
                return new FeldNachschlagErgebnis.NichtGefunden(
                    $"Der Abwasserkataster fuehrt fuer {anfrage.Feldname} keinen Wert.");
            }

            return werte.Count == 1
                ? new FeldNachschlagErgebnis.Gefunden(werte[0])
                : new FeldNachschlagErgebnis.Mehrdeutig(werte);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FeldNachschlagErgebnis.Fehler(ex.Message);
        }
    }

    private List<CadastreHaltung> SucheHaltungen(string? name)
    {
        if (!_store.IsTableFresh(_tabellenPfad, _xtfPfad))
            _store.BuildTable(_xtfPfad, _tabellenPfad);

        var gesucht = (name ?? string.Empty).Trim();
        if (gesucht.Length == 0)
            return [];

        return _store.ReadTable(_tabellenPfad)
            .Where(h => string.Equals(
                h.Bezeichnung?.Trim(), gesucht, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private string? LiesFeld(CadastreHaltung haltung, string feldname) => feldname switch
    {
        "Rohrmaterial" => haltung.Material,
        "Haltungslaenge_m" => haltung.Laenge,
        var f when f.StartsWith("Eigent", StringComparison.OrdinalIgnoreCase)
            => _leseOrganisation(_xtfPfad),
        _ => null
    };
}
