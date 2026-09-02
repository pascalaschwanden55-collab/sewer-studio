using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Die Organisationen einer SIA405-XTF und der Weg vom Eigentuemer zu ihrer Kennung.
///
/// In SIA405 ist der Eigentuemer kein Text, sondern ein Verweis auf ein Objekt der Klasse
/// <c>Organisation</c> im Topic <c>Administration</c>. Im Kantonsexport von Abwasser Uri
/// gibt es genau eine solche Organisation, und alle 174291 Objekte zeigen auf sie —
/// deshalb steht dort bei jedem Objekt derselbe Eigentuemer, unabhaengig davon, was im
/// Kataster steht.
///
/// Fehlt eine Organisation, wird sie angelegt. Zwei Sperren dagegen: Fuehrt die Datei
/// ueberhaupt keine Organisation, wird auch keine erfunden — dann fehlt das ganze Topic,
/// und eines anzulegen waere ein Eingriff in den Aufbau der Kundendatei. Und ohne
/// bekannten <c>Organisationstyp</c> entsteht nichts, weil das Feld in SIA405 Pflicht ist
/// und ein geratener Typ eine Aussage waere, die niemand getroffen hat.
///
/// Haltungen und Schaechte teilen sich EIN Buch je Datei. Zwei getrennte Buecher wuerden
/// dieselbe Kennung zweimal vergeben oder dieselbe Organisation doppelt anlegen.
///
/// Reine Rechnung ohne Dateizugriff und ohne Mutation der XTF.
/// </summary>
public sealed class XtfOrganisationsbuch
{
    private readonly Dictionary<string, string> _jeBezeichnung = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _vergebeneTids = new(StringComparer.Ordinal);
    private readonly List<XtfNeueOrganisation> _neue = [];
    private readonly bool _fuehrtOrganisationen;
    private readonly string _tidPraefix;

    public XtfOrganisationsbuch(IReadOnlyList<XtfStammdatenElement> elemente)
    {
        ArgumentNullException.ThrowIfNull(elemente);

        foreach (var element in elemente)
        {
            _vergebeneTids.Add(element.Tid);

            if (!string.Equals(element.Klasse, "Organisation", StringComparison.Ordinal))
                continue;

            _fuehrtOrganisationen = true;
            var bezeichnung = (element.Bezeichnung ?? "").Trim();
            if (bezeichnung.Length > 0)
                _jeBezeichnung.TryAdd(bezeichnung, element.Tid);
        }

        // Die Kennungen neuer Objekte folgen der Schreibweise der Datei.
        var vorlage = elemente
            .Select(e => e.Tid)
            .FirstOrDefault(t => t.StartsWith("ch", StringComparison.OrdinalIgnoreCase));
        _tidPraefix = vorlage is { Length: >= 8 } ? vorlage[..8] : "chORG000";
    }

    /// <summary>Organisationen, die der Ausfuehrer noch anlegen muss.</summary>
    public IReadOnlyList<XtfNeueOrganisation> Neue => _neue;

    /// <summary>
    /// Der Verweis auf die Organisation des Eigentuemers, oder <c>null</c>, wenn nichts zu
    /// aendern ist. Ein Grund fuer ein Nein landet in <paramref name="hinweise"/>.
    /// </summary>
    public XtfRevisionFeld? Verweis(
        XtfStammdatenElement? element,
        string name,
        string? eigentuemer,
        List<string> hinweise)
    {
        ArgumentNullException.ThrowIfNull(hinweise);

        var roh = (eigentuemer ?? "").Trim();
        if (element is null || roh.Length == 0)
            return null;

        var bezeichnung = EigentumVokabular.Normalisieren(roh);
        if (!_jeBezeichnung.TryGetValue(bezeichnung, out var tid))
        {
            if (!_fuehrtOrganisationen)
            {
                hinweise.Add(
                    $"{name}: die XTF fuehrt keine Organisationen — der Eigentuemer " +
                    $"\"{bezeichnung}\" bleibt aussen vor.");
                return null;
            }

            var typ = EigentumVokabular.NachOrganisationstyp(bezeichnung);
            if (typ is null)
            {
                hinweise.Add(
                    $"{name}: fuer den Eigentuemer \"{bezeichnung}\" ist kein " +
                    "Organisationstyp nach SIA405 bekannt — nicht geschrieben.");
                return null;
            }

            tid = NaechsteTid();
            _jeBezeichnung[bezeichnung] = tid;
            _neue.Add(new XtfNeueOrganisation(tid, bezeichnung, typ));
        }

        element.Werte.TryGetValue("EigentuemerRef", out var alt);
        alt = (alt ?? "").Trim();
        return string.Equals(alt, tid, StringComparison.Ordinal)
            ? null
            : new XtfRevisionFeld("EigentuemerRef", alt.Length == 0 ? null : alt, tid, IstVerweis: true);
    }

    private string NaechsteTid()
    {
        for (var i = 1; i < 1_000_000; i++)
        {
            var kandidat = $"{_tidPraefix}O{i:D6}";
            if (_vergebeneTids.Add(kandidat))
                return kandidat;
        }

        throw new InvalidOperationException("Es konnte keine freie Kennung vergeben werden.");
    }
}
